using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DekerekeToPa;

namespace PaDekereke
{
	/// <summary>
	/// The one piece of UI a Dekereke user ever sees: their database's actual
	/// column names on the left, a Phonology Assistant field (or "not imported")
	/// on the right, pre-filled by the auto-mapper. Shown once per database, and
	/// again only on demand (SHIFT held while the project loads).
	///
	/// Deliberately code-only WinForms (no .Designer.cs/.resx) to stay reviewable.
	/// </summary>
	public sealed class MappingDialog : Form
	{
		private const string NotImported = "(not imported)";

		private readonly DekerekeDatabase _db;
		private readonly DataGridView _grid;
		private readonly Label _status;
		private readonly Button _ok;

		/// <summary>The confirmed map. Valid only after ShowDialog() == OK.</summary>
		public ColumnMap Result { get; private set; }

		public MappingDialog(DekerekeDatabase db, ColumnMap initial)
		{
			_db = db;

			Text = "Dekereke Data Source - Field Mapping";
			StartPosition = FormStartPosition.CenterScreen;
			MinimizeBox = false;
			ShowIcon = false;
			ShowInTaskbar = false;
			ClientSize = new Size(520, 480);
			MinimumSize = new Size(420, 320);

			var header = new Label
			{
				Dock = DockStyle.Top,
				Height = 48,
				Padding = new Padding(8, 8, 8, 0),
				Text = "Choose which Phonology Assistant field each Dekereke column supplies.\r\n" +
					"Database: " + db.SourcePath
			};

			_grid = new DataGridView
			{
				Dock = DockStyle.Fill,
				AllowUserToAddRows = false,
				AllowUserToDeleteRows = false,
				AllowUserToResizeRows = false,
				RowHeadersVisible = false,
				AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
				SelectionMode = DataGridViewSelectionMode.CellSelect,
				EditMode = DataGridViewEditMode.EditOnEnter
			};

			var colDekereke = new DataGridViewTextBoxColumn
			{
				HeaderText = "Dekereke column",
				ReadOnly = true,
				FillWeight = 55
			};

			var colField = new DataGridViewComboBoxColumn
			{
				HeaderText = "Phonology Assistant field",
				FillWeight = 45,
				FlatStyle = FlatStyle.Flat
			};
			colField.Items.Add(NotImported);
			foreach (var f in PaFieldNames.All)
				colField.Items.Add(f);

			_grid.Columns.Add(colDekereke);
			_grid.Columns.Add(colField);

			foreach (var column in db.Columns)
			{
				var mapped = initial == null ? null :
					initial.Mappings.FirstOrDefault(m => m.Column == column);
				_grid.Rows.Add(column, mapped == null ? NotImported : mapped.PaField);
			}

			_status = new Label
			{
				Dock = DockStyle.Bottom,
				Height = 24,
				Padding = new Padding(8, 4, 8, 0),
				ForeColor = Color.Firebrick
			};

			var buttons = new FlowLayoutPanel
			{
				Dock = DockStyle.Bottom,
				FlowDirection = FlowDirection.RightToLeft,
				Height = 40,
				Padding = new Padding(8)
			};

			var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
			_ok = new Button { Text = "OK" };
			_ok.Click += HandleOkClick;
			buttons.Controls.Add(cancel);
			buttons.Controls.Add(_ok);

			Controls.Add(_grid);
			Controls.Add(_status);
			Controls.Add(buttons);
			Controls.Add(header);

			AcceptButton = _ok;
			CancelButton = cancel;

			_grid.CurrentCellDirtyStateChanged += delegate
			{
				if (_grid.IsCurrentCellDirty)
					_grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
				UpdateValidation();
			};

			UpdateValidation();
		}

		private ColumnMap BuildMap(out string error)
		{
			error = null;
			var map = new ColumnMap();
			var usedFields = new Dictionary<string, string>(); // field -> column

			foreach (DataGridViewRow row in _grid.Rows)
			{
				var column = (string)row.Cells[0].Value;
				var field = row.Cells[1].Value as string;

				if (string.IsNullOrEmpty(field) || field == NotImported)
					continue;

				string alreadyBy;
				if (usedFields.TryGetValue(field, out alreadyBy))
				{
					error = string.Format("'{0}' and '{1}' are both mapped to {2}.",
						alreadyBy, column, field);
					return null;
				}

				usedFields[field] = column;
				map.Mappings.Add(new ColumnMapping(column, field));
			}

			if (!map.HasPhonetic)
			{
				error = "One column must be mapped to Phonetic.";
				return null;
			}

			return map;
		}

		private void UpdateValidation()
		{
			string error;
			var map = BuildMap(out error);
			_status.Text = error ?? string.Empty;
			_ok.Enabled = map != null;
		}

		private void HandleOkClick(object sender, EventArgs e)
		{
			string error;
			var map = BuildMap(out error);
			if (map == null)
			{
				_status.Text = error;
				return;
			}

			Result = map;
			DialogResult = DialogResult.OK;
			Close();
		}
	}
}
