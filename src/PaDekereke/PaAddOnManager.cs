// Dekereke Data Sources for Phonology Assistant
// Copyright (C) 2026 Seth Johnston
//
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// This program is free software: you can redistribute it and/or modify it
// under the terms of the GNU Affero General Public License as published by
// the Free Software Foundation, either version 3 of the License, or (at your
// option) any later version.
//
// This program is distributed in the hope that it will be useful, but WITHOUT
// ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or
// FITNESS FOR A PARTICULAR PURPOSE. See the GNU Affero General Public License
// for more details.
//
// You should have received a copy of the GNU Affero General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
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

		private static PaAddOnManager s_instance;

		// Loads can NEST: a modal dialog shown inside OnBeforeLoadingDataSources
		// pumps messages, and PA can start a second complete load pipeline from
		// inside it (observed live, 2026-08-10). Per-load swap lists therefore
		// live on a stack, paired LIFO with each Before/After broadcast.
		private readonly Stack<List<Swap>> m_swapStack = new Stack<List<Swap>>();

		// Dekereke paths whose mapping dialog is open right now, so a nested
		// load of the same database silently uses the auto-map instead of
		// stacking a second identical dialog on top of the first.
		private static readonly HashSet<string> s_dialogOpenFor =
			new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		/// ------------------------------------------------------------------------------------
		public PaAddOnManager()
		{
			// PA runs ReadAddOns once per main-window construction, so this class
			// is instantiated again every time a project window opens (observed
			// live, 2026-08-10). Only the first instance registers; later ones
			// stand down, or every load would be handled - and prompted - twice.
			if (s_instance != null)
			{
				Log("constructed again (PA re-ran ReadAddOns); duplicate instance standing down");
				return;
			}
			s_instance = this;

			// PA swallows every add-on exception, so failure is silent by design;
			// this line is the proof the assembly loaded and the loader ran us.
			Log("constructed (assembly loaded, PaAddOnManager instantiated by PA)");

			try
			{
				RegisterWithPa();
			}
			catch (Exception ex)
			{
				// An add-on must never take PA down. Typical cause of landing here:
				// the reference to Pa.exe/SilTools.dll did not bind at run time.
				Log("FAILED to register with PA: " + ex);
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Everything that touches PA types, kept out of the constructor (and not
		/// inlined back into it) so the constructor always JITs and logs even when
		/// the Pa.exe/SilTools.dll references fail to bind - the binding failure
		/// then surfaces as a logged exception instead of pure silence.
		/// </summary>
		[MethodImpl(MethodImplOptions.NoInlining)]
		private void RegisterWithPa()
		{
			// If a future PA handles Dekereke natively, stand down entirely.
			if (Enum.GetNames(typeof(DataSourceType)).Contains("Dekereke"))
			{
				Log("PA has native Dekereke support; add-on standing down");
				return;
			}

			App.AddMediatorColleague(this);
			Log("registered with PA mediator; waiting for BeforeLoadingDataSources");
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

			Log("BeforeLoadingDataSources: project '" + project.Name + "', " +
				project.DataSources.Count + " data source(s)");

			var swaps = new List<Swap>();
			m_swapStack.Push(swaps);

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
						swaps.Add(swap);
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
			if (project == null || m_swapStack.Count == 0)
				return false;

			// LIFO pairing with OnBefore matches the nested-load order observed
			// live (Before1, Before2, After2, After1). Restore is swap-driven
			// (each Swap carries its own object references), so even an
			// out-of-order pairing restores the right entries.
			var swaps = m_swapStack.Pop();
			bool restoredAny = false;

			foreach (var swap in swaps)
			{
				try
				{
					RestoreSwap(project, swap);
					restoredAny = true;
					Log("restored '" + swap.DekerekePath + "' as the project data source");
				}
				catch (Exception ex)
				{
					ShowError(swap.DekerekePath, ex);
				}
			}

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
			Log("converted '" + dekerekePath + "': " + result.RecordsWritten +
				" record(s) written, " + result.RecordsSkippedNoPhonetic +
				" skipped (no phonetic)");

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
				// A second load can start inside this dialog's message pump (PA
				// re-enters; see the nested-load note on m_swapStack). Never stack
				// a second identical dialog: the nested pass silently uses the
				// auto-map, and the outer pass - whose read finishes last and
				// wins - carries the user's confirmed result.
				if (!s_dialogOpenFor.Add(db.SourcePath))
				{
					Log("mapping dialog already open for this database; nested load uses the auto-map");
					return map.HasPhonetic ? map : null;
				}

				try
				{
					Log("showing mapping dialog (" +
						(isNew ? "first contact" :
						!map.HasPhonetic ? "phonetic unmapped" : "Shift held") + ")");
					App.CloseSplashScreen();

					using (var dlg = new MappingDialog(db, map))
					{
						if (dlg.ShowDialog() != DialogResult.OK)
						{
							Log("mapping dialog cancelled");
							return isNew ? null : map; // cancel: keep old map, or skip if none
						}

						map = dlg.Result;
						changed = true;
						Log("mapping dialog confirmed (" + map.Mappings.Count + " column(s) mapped)");
					}
				}
				finally
				{
					s_dialogOpenFor.Remove(db.SourcePath);
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
		/// <summary>
		/// The only external evidence of life in a host that swallows all add-on
		/// errors: appends to %LOCALAPPDATA%\PaDekereke\addon.log. Never throws,
		/// and deliberately does not depend on any PA or DekerekeToPa type so it
		/// works even when those references fail to bind.
		/// </summary>
		private static void Log(string message)
		{
			try
			{
				var dir = Path.Combine(
					Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
					"PaDekereke");
				Directory.CreateDirectory(dir);
				File.AppendAllText(Path.Combine(dir, "addon.log"),
					DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message +
					Environment.NewLine);
			}
			catch
			{
				// Logging must never become the failure.
			}
		}

		/// ------------------------------------------------------------------------------------
		private static void ShowError(string sourcePath, Exception ex)
		{
			try
			{
				Log("ERROR processing '" + sourcePath + "': " + ex);
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
