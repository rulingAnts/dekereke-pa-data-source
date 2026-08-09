using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DekerekeToPa;
using SIL.Pa;
using SIL.Pa.DataSource;
using SIL.Pa.Model;
using SilTools;

namespace PaDekereke
{
	/// <summary>
	/// Phonology Assistant add-on that makes Dekereke databases usable as live,
	/// auto-refreshing PA data sources.
	///
	/// How it works (all hook points verified against PA 4.1.1 source,
	/// sillsdev/phonology-assistant @ master):
	///
	///  - PA's add-on loader (App.cs:367) instantiates this class at startup
	///    because it is named "PaAddOnManager".
	///  - PaProject.LoadDataSources (PaProject.cs:530) broadcasts
	///    "BeforeLoadingDataSources" immediately before reading, and
	///    "AfterLoadingDataSources" after. The shelved PaDataSourceUtilsAddOn in
	///    the PA repo uses the same two messages the same way.
	///  - On Before: every data source whose file is Dekereke XML is converted to
	///    Toolbox SFM in a cache folder, and the project's DataSources list entry
	///    is temporarily replaced with a fully-mapped SFM data source.
	///  - On After: the original entries are restored, so the .pap keeps pointing
	///    at the Dekereke file. That is what makes refresh automatic: PA's
	///    focus-regained check (PaProject.CheckForModifiedDataSources) watches the
	///    Dekereke file's timestamp and re-runs the whole load - including this
	///    add-on - whenever Dekereke saves.
	///
	/// The column-to-field mapping is auto-guessed on first use (Dekereke columns
	/// are user-defined per database), confirmable in a small dialog, then stored
	/// per project. It never prompts again on routine reloads; hold SHIFT while PA
	/// loads the project to reopen the mapping dialog.
	/// </summary>
	public class PaAddOnManager : IxCoreColleague
	{
		private sealed class Swap
		{
			public int Index;
			public PaDataSource Original;
			public PaDataSource Temp;
			public DateTime DekerekeMTimeUtc;
			public string DekerekePath;
		}

		private List<Swap> m_swaps;

		/// ------------------------------------------------------------------------------------
		public PaAddOnManager()
		{
			try
			{
				// If a future PA handles Dekereke natively, stand down entirely.
				if (Enum.GetNames(typeof(DataSourceType)).Contains("Dekereke"))
					return;

				App.AddMediatorColleague(this);
			}
			catch
			{
				// An add-on must never take PA down.
			}
		}

		#region IxCoreColleague
		/// ------------------------------------------------------------------------------------
		public IxCoreColleague[] GetMessageTargets()
		{
			return new IxCoreColleague[] { this };
		}

		#endregion

		#region Message handlers (dispatched by name via SilTools.Mediator)
		/// ------------------------------------------------------------------------------------
		protected bool OnBeforeLoadingDataSources(object args)
		{
			var project = args as PaProject;
			if (project == null || project.DataSources == null)
				return false;

			m_swaps = new List<Swap>();

			// Hold SHIFT during project load to force the mapping dialog open.
			bool forceDialog = (Control.ModifierKeys & Keys.Shift) == Keys.Shift;

			for (int i = 0; i < project.DataSources.Count; i++)
			{
				var ds = project.DataSources[i];

				try
				{
					if (ds.SkipLoading || ds.SkipLoadingBecauseOfProblem)
						continue;

					var path = ds.SourceFile;
					if (string.IsNullOrEmpty(path) || !File.Exists(path) || !DekerekeFile.Sniff(path))
						continue;

					var swap = ConvertAndSwap(project, i, ds, forceDialog);
					if (swap != null)
						m_swaps.Add(swap);
				}
				catch (Exception ex)
				{
					// Leave this data source untouched; PA will fall back to its own
					// (unhelpful but harmless) handling of the raw XML file.
					ShowError(ds.SourceFile, ex);
				}
			}

			return false; // let other colleagues see the message too
		}

		/// ------------------------------------------------------------------------------------
		protected bool OnAfterLoadingDataSources(object args)
		{
			var project = args as PaProject;
			if (project == null || m_swaps == null)
				return false;

			bool restoredAny = false;

			foreach (var swap in m_swaps)
			{
				try
				{
					RestoreSwap(project, swap);
					restoredAny = true;
				}
				catch (Exception ex)
				{
					ShowError(swap.DekerekePath, ex);
				}
			}

			m_swaps = null;

			// PA can save the .pap DURING the load (ProjectInventoryBuilder.cs:221
			// saves on the first-ever load of a project). If that happened, the temp
			// SFM path was baked into the .pap. Saving again now, after restoring,
			// rewrites it with the true Dekereke path.
			if (restoredAny)
			{
				try { project.Save(); }
				catch { /* non-fatal: the next normal save fixes it */ }
			}

			return false;
		}

		#endregion

		#region Swap machinery
		/// ------------------------------------------------------------------------------------
		private Swap ConvertAndSwap(PaProject project, int index, PaDataSource ds, bool forceDialog)
		{
			var dekerekePath = ds.SourceFile;
			var db = DekerekeFile.Read(dekerekePath);

			var map = GetOrCreateMap(project, db, forceDialog);
			if (map == null || !map.HasPhonetic)
				return null; // user cancelled a first-time mapping; skip this source

			var sfmPath = ConversionCache.GetCachePathFor(dekerekePath);
			var result = SfmWriter.Write(db, map, sfmPath);

			// Build the replacement data source. The PaDataSource(fields, filename)
			// constructor sniffs the file: XML parse fails on SFM text, the \_sh
			// header wins, so Type comes out Toolbox and SfmRecordMarker is picked
			// up from PA's DefaultSfmRecordMarker (\ref). Then our own complete
			// mappings replace the defaults - including fields PA could never
			// auto-map because they have no recognized markers (\pm, \or, \nt).
			var temp = new PaDataSource(project.Fields, sfmPath);
			temp.FieldMappings = BuildFieldMappings(project, map);
			temp.SfmRecordMarker = "\\ref";
			temp.TotalLinesInFile = result.TotalLines;

			var swap = new Swap
			{
				Index = index,
				Original = ds,
				Temp = temp,
				DekerekePath = dekerekePath,
				DekerekeMTimeUtc = File.GetLastWriteTimeUtc(dekerekePath)
			};

			project.DataSources[index] = temp;
			return swap;
		}

		/// ------------------------------------------------------------------------------------
		private static void RestoreSwap(PaProject project, Swap swap)
		{
			// Put the original Dekereke data source back. Find it defensively in
			// case another add-on reordered the list.
			if (swap.Index < project.DataSources.Count &&
				ReferenceEquals(project.DataSources[swap.Index], swap.Temp))
			{
				project.DataSources[swap.Index] = swap.Original;
			}
			else
			{
				var idx = project.DataSources.IndexOf(swap.Temp);
				if (idx >= 0)
					project.DataSources[idx] = swap.Original;
			}

			// Stamp the Dekereke file's mtime (captured at conversion time) so PA's
			// focus-regained check compares against the right baseline: a Dekereke
			// save AFTER this load must trigger a reload; this load itself must not.
			swap.Original.LastModification = swap.DekerekeMTimeUtc;

			// Cosmetic: records created during the load keep a reference to the temp
			// data source object. Point its SourceFile back at the Dekereke file so
			// the DataSource/DataSourcePath columns in the UI show the real file.
			// (Reading is finished; nothing re-reads this object.)
			swap.Temp.SourceFile = swap.DekerekePath;
		}

		/// ------------------------------------------------------------------------------------
		private static List<FieldMapping> BuildFieldMappings(PaProject project, ColumnMap map)
		{
			var mappings = new List<FieldMapping>();

			foreach (var m in map.Mappings)
			{
				var field = project.Fields.FirstOrDefault(f => f.Name == m.PaField);
				if (field == null)
					continue; // project lacks this field; skip quietly

				string marker;
				if (!PaFieldNames.SfmMarkers.TryGetValue(m.PaField, out marker))
					continue;

				// FieldMapping(nameInSource, field, isParsed) - the marker string is
				// what PA matches against lines in the SFM file.
				mappings.Add(new FieldMapping(marker, field,
					PaFieldNames.ParsedByDefault.Contains(m.PaField)));
			}

			return mappings;
		}

		#endregion

		#region Mapping persistence + dialog
		/// ------------------------------------------------------------------------------------
		private static string GetStorePath(PaProject project)
		{
			return Path.Combine(project.Folder, project.Name + ".DekerekeMappings.xml");
		}

		/// ------------------------------------------------------------------------------------
		private static ColumnMap GetOrCreateMap(PaProject project, DekerekeDatabase db,
			bool forceDialog)
		{
			var storePath = GetStorePath(project);
			var store = MappingStore.Load(storePath);
			var map = store.FindFor(db.SourcePath);
			bool isNew = map == null;
			bool changed = false;

			if (isNew)
			{
				map = AutoMapper.Map(db.Columns);
				changed = true;
			}
			else
			{
				// Columns added in Dekereke since the map was saved get auto-mapped
				// silently - this path runs on every focus-triggered reload and must
				// never prompt.
				changed = AutoMapper.MapNewColumns(map, db.Columns);
			}

			// Prompt only on first contact, when phonetic is unresolvable, or on
			// explicit request (SHIFT held during load).
			if (isNew || !map.HasPhonetic || forceDialog)
			{
				App.CloseSplashScreen();

				using (var dlg = new MappingDialog(db, map))
				{
					if (dlg.ShowDialog() != DialogResult.OK)
						return isNew ? null : map; // cancel: keep old map, or skip if none

					map = dlg.Result;
					changed = true;
				}
			}

			if (changed)
			{
				store.SetFor(db.SourcePath, map);
				try { store.Save(storePath); }
				catch { /* read-only project folder: run with the in-memory map */ }
			}

			return map;
		}

		/// ------------------------------------------------------------------------------------
		private static void ShowError(string sourcePath, Exception ex)
		{
			try
			{
				MessageBox.Show(
					string.Format(
						"The Dekereke add-on could not process '{0}'.{1}{1}{2}",
						sourcePath, Environment.NewLine, ex.Message),
					"Dekereke Data Sources",
					MessageBoxButtons.OK, MessageBoxIcon.Warning);
			}
			catch
			{
				// Headless or shutting down - swallow.
			}
		}

		#endregion
	}
}
