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
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using DekerekeToPa;
using SIL.Pa.DataSource;
using SIL.Pa.Model;

namespace PaDekereke
{
	/// <summary>
	/// Adds "Dekereke Data Source..." to the Add dropdown of PA's project
	/// settings dialog - including the NEW PROJECT wizard, which is where a
	/// user naturally reaches for it.
	///
	/// Why this needs reflection. That dropdown is not part of PA's
	/// toolbar/menu adapter (which add-ons may extend through ITMAdapter, as
	/// this add-on does for the Tools menu). It is a plain WinForms
	/// ContextMenuStrip declared as a private field 'mnuAdd' inside
	/// ProjectSettingsDlg (ProjectSettingsDlg.Designer.cs:641), shown by hand
	/// under the Add button (ProjectSettingsDlg.cs:722). PA offers no hook for
	/// it, so the add-on finds the live dialog and injects an item.
	///
	/// Failure mode by design: every lookup below is null-checked and every
	/// entry point swallows exceptions. If a future PA renames a field, the
	/// menu item simply does not appear (and the reason is logged) - PA itself
	/// is never destabilized. The Tools menu item and, failing that, PA's own
	/// project settings remain usable.
	///
	/// Verified against PA 4.1.1 source (sillsdev/phonology-assistant @ master).
	/// </summary>
	internal static class ProjectSettingsDlgPatcher
	{
		private const string DialogTypeName = "ProjectSettingsDlg";
		private const string AddMenuFieldName = "mnuAdd";
		private const string DataSourcesFieldName = "_dataSources";
		private const string GridFieldName = "m_grid";
		private const string LoadGridMethodName = "LoadGrid";
		private const string OtherSourceItemName = "mnuAddOtherDataSource";
		private const string OurItemName = "mnuAddDekerekeDataSourceInDlg";

		/// <summary>
		/// PA's OK button refuses an XML data source with no XSLT file
		/// (ProjectSettingsDlg.cs:362). Nothing ever READS this value: PA's
		/// reader skips XML sources outright (DataSourceReader.cs:274), the
		/// grid column showing it is hidden (ProjectSettingsDlg.cs:228), and
		/// the only other use is PaDataSource.Copy(). Setting it to a non-empty
		/// marker is therefore exactly enough to get a Dekereke source past
		/// validation, and is inert everywhere else. The add-on converts and
		/// swaps the source before any read, so no XSLT is ever wanted.
		/// </summary>
		internal const string XsltPlaceholder = "(none - converted by the Dekereke add-on)";

		private static bool s_watching;
		private static bool s_loggedInjectFailure;

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Starts watching for the dialog. Application.Idle is raised by the
		/// message loop whenever the queue empties - including the nested loop
		/// a modal dialog runs - so the dialog is caught right after it opens,
		/// with no polling timer and no dependence on mediator message order.
		/// </summary>
		internal static void Start()
		{
			if (s_watching)
				return;

			s_watching = true;
			Application.Idle += HandleApplicationIdle;
		}

		/// ------------------------------------------------------------------------------------
		private static void HandleApplicationIdle(object sender, EventArgs e)
		{
			try
			{
				// Indexed loop: the collection can change while a form is
				// opening or closing, and enumerating it then can throw.
				var forms = Application.OpenForms;
				for (int i = 0; i < forms.Count; i++)
				{
					var form = i < forms.Count ? forms[i] : null;
					if (form != null && form.GetType().Name == DialogTypeName)
						TryInject(form);
				}
			}
			catch
			{
				// Never let a background sweep disturb PA.
			}
		}

		/// ------------------------------------------------------------------------------------
		private static void TryInject(Form dlg)
		{
			try
			{
				var menu = GetField(dlg, AddMenuFieldName) as ContextMenuStrip;
				if (menu == null)
				{
					LogInjectFailureOnce("the dialog has no '" + AddMenuFieldName + "' menu field");
					return;
				}

				if (menu.Items.ContainsKey(OurItemName))
					return; // already injected into this dialog instance

				// Bail out before touching the UI if the members the click
				// handler needs are missing - better no menu item than one
				// that fails when clicked.
				if (GetField(dlg, DataSourcesFieldName) as List<PaDataSource> == null ||
					GetProperty(dlg, "Project") as PaProject == null)
				{
					LogInjectFailureOnce("the dialog's data source list or project is not reachable");
					return;
				}

				var item = new ToolStripMenuItem("&Dekereke Data Source...") { Name = OurItemName };
				item.ToolTipText = "Add a Dekereke phonology database (.xml) that stays live: " +
					"edit it in Dekereke and Phonology Assistant refreshes by itself.";
				item.Click += delegate { HandleAddClick(dlg); };

				// Group it with the other non-FieldWorks source, directly below it.
				var other = menu.Items[OtherSourceItemName];
				var index = other == null ? menu.Items.Count : menu.Items.IndexOf(other) + 1;
				menu.Items.Insert(index, item);

				PaAddOnManager.Log("added 'Dekereke Data Source...' to the project settings Add menu");
			}
			catch (Exception ex)
			{
				LogInjectFailureOnce(ex.ToString());
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Mirrors PA's own HandleAddOtherDataSourceClick
		/// (ProjectSettingsDlg.cs:604): add to the dialog's pending list, then
		/// reload the grid. Nothing is written until the user clicks OK, so
		/// Cancel discards this exactly like any other data source.
		/// </summary>
		private static void HandleAddClick(Form dlg)
		{
			try
			{
				var project = GetProperty(dlg, "Project") as PaProject;
				var dataSources = GetField(dlg, DataSourcesFieldName) as List<PaDataSource>;

				if (project == null || dataSources == null)
				{
					ShowMessage(dlg, "This version of Phonology Assistant is not compatible with " +
						"the Dekereke add-on's project settings integration.", MessageBoxIcon.Error);
					return;
				}

				string path;
				using (var ofd = new OpenFileDialog())
				{
					ofd.Title = "Choose a Dekereke Database";
					ofd.Filter = "Dekereke databases (*.xml)|*.xml|All files (*.*)|*.*";
					ofd.CheckFileExists = true;

					if (ofd.ShowDialog(dlg) != DialogResult.OK)
						return;

					path = ofd.FileName;
				}

				// Confirm it really is a Dekereke database (well-formed XML whose
				// root element is <phon_data>) rather than trusting the extension.
				if (!DekerekeFile.Sniff(path))
				{
					PaAddOnManager.Log("project settings dialog: rejected non-Dekereke file " + path);
					ShowMessage(dlg, string.Format(
						"'{0}' is not a Dekereke database.{1}{1}A Dekereke database is an XML file " +
						"whose top-level element is <phon_data>.", path, Environment.NewLine),
						MessageBoxIcon.Exclamation);
					return;
				}

				if (dataSources.Any(d => string.Equals(d.SourceFile, path, StringComparison.OrdinalIgnoreCase)))
				{
					ShowMessage(dlg, string.Format(
						"'{0}' is already a data source in this project.", path),
						MessageBoxIcon.Information);
					return;
				}

				// Confirms the mapping and produces a source PA's OK-button
				// validation accepts (it demands a phonetic FieldMapping, which
				// the PaDataSource ctor never builds for an XML file).
				PaDataSource ds;
				if (!PaAddOnManager.TryCreateDekerekeDataSource(project, path, dlg, out ds))
					return;

				dataSources.Add(ds);

				RefreshGrid(dlg);
				PaAddOnManager.Log("project settings dialog: added Dekereke data source " + path);
			}
			catch (Exception ex)
			{
				PaAddOnManager.Log("ERROR adding Dekereke data source from project settings: " + ex);
				ShowMessage(dlg, "The Dekereke add-on could not add that file." +
					Environment.NewLine + Environment.NewLine + ex.Message, MessageBoxIcon.Warning);
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Same sequence PA uses after adding a source: LoadGrid is passed the
		/// grid's CURRENT row count, which - the grid not yet being reloaded -
		/// is the index of the row about to appear, so the new row ends up
		/// current. Grid refresh failing is cosmetic: the data source is
		/// already in the list and OK will still commit it.
		/// </summary>
		private static void RefreshGrid(Form dlg)
		{
			try
			{
				var grid = GetField(dlg, GridFieldName) as DataGridView;
				if (grid == null)
					return;

				var loadGrid = dlg.GetType().GetMethod(LoadGridMethodName,
					BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
					null, new[] { typeof(int) }, null);

				if (loadGrid != null)
					loadGrid.Invoke(dlg, new object[] { grid.Rows.Count });

				grid.Focus();

				// SilGrid.IsDirty - marks the dialog's grid as edited.
				var isDirty = grid.GetType().GetProperty("IsDirty",
					BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (isDirty != null && isDirty.CanWrite)
					isDirty.SetValue(grid, true, null);
			}
			catch (Exception ex)
			{
				PaAddOnManager.Log("could not refresh the project settings grid: " + ex.Message);
			}
		}

		#region Reflection and message helpers
		/// ------------------------------------------------------------------------------------
		private static object GetField(object target, string name)
		{
			if (target == null)
				return null;

			// Walk the hierarchy: private fields are not returned for base types.
			for (var type = target.GetType(); type != null; type = type.BaseType)
			{
				var field = type.GetField(name,
					BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

				if (field != null)
					return field.GetValue(target);
			}

			return null;
		}

		/// ------------------------------------------------------------------------------------
		private static object GetProperty(object target, string name)
		{
			if (target == null)
				return null;

			for (var type = target.GetType(); type != null; type = type.BaseType)
			{
				var prop = type.GetProperty(name,
					BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

				if (prop != null && prop.CanRead)
					return prop.GetValue(target, null);
			}

			return null;
		}

		/// ------------------------------------------------------------------------------------
		private static void ShowMessage(Form owner, string text, MessageBoxIcon icon)
		{
			try
			{
				MessageBox.Show(owner, text, "Dekereke Data Sources", MessageBoxButtons.OK, icon);
			}
			catch
			{
				// Shutting down - nothing useful to do.
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>Logs once: Application.Idle would otherwise repeat it forever.</summary>
		private static void LogInjectFailureOnce(string reason)
		{
			if (s_loggedInjectFailure)
				return;

			s_loggedInjectFailure = true;
			PaAddOnManager.Log("could NOT add the Dekereke item to the project settings Add menu (" +
				reason + "); the Tools menu item still works");
		}

		#endregion
	}
}
