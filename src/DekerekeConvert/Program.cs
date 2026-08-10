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
using System.IO;
using System.Linq;
using DekerekeToPa;

namespace DekerekeConvert
{
	/// <summary>
	/// Converts a Dekereke database to a Toolbox SFM snapshot that Phonology
	/// Assistant imports natively, using exactly the reader, auto-mapper and
	/// writer the PaDekereke add-on uses. A diagnostic/test vehicle: it proves
	/// the conversion pipeline against a real PA without the add-on's live hook.
	/// </summary>
	internal static class Program
	{
		private static int Main(string[] args)
		{
			if (args.Length < 1 || args.Length > 2)
			{
				Console.WriteLine("Usage: DekerekeConvert <dekereke.xml> [output.db]");
				Console.WriteLine();
				Console.WriteLine("Converts a Dekereke database to a Toolbox SFM file for");
				Console.WriteLine("Phonology Assistant, printing the auto-guessed column mapping.");
				Console.WriteLine("Default output: <dekereke name>-pa.db next to the input.");
				return 2;
			}

			try
			{
				return Convert(args[0], args.Length == 2 ? args[1] : null);
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine("FAILED: " + ex.Message);
				return 1;
			}
		}

		private static int Convert(string input, string output)
		{
			if (!File.Exists(input))
			{
				Console.Error.WriteLine("File not found: " + input);
				return 1;
			}

			if (!DekerekeFile.Sniff(input))
			{
				Console.Error.WriteLine(
					"Not a Dekereke database (root element is not <phon_data>): " + input);
				return 1;
			}

			if (output == null)
			{
				output = Path.Combine(
					Path.GetDirectoryName(Path.GetFullPath(input)) ?? ".",
					Path.GetFileNameWithoutExtension(input) + "-pa.db");
			}

			if (string.Equals(Path.GetFullPath(output), Path.GetFullPath(input),
				StringComparison.OrdinalIgnoreCase))
			{
				Console.Error.WriteLine("Output would overwrite the input; pick another name.");
				return 1;
			}

			var db = DekerekeFile.Read(input);
			Console.WriteLine("Read " + db.Records.Count + " record(s), " +
				db.Columns.Count + " column(s).");
			if (db.SoundFilePath != null)
				Console.WriteLine("Audio folder (from DkUserSettings): " + db.SoundFilePath);

			var map = AutoMapper.Map(db.Columns);

			Console.WriteLine();
			Console.WriteLine("Auto-guessed mapping (PA field <- Dekereke column):");
			foreach (var m in map.Mappings)
				Console.WriteLine("  {0,-16} <- {1}", m.PaField, m.Column);

			var unmapped = db.Columns
				.Where(c => map.Mappings.All(m => m.Column != c)).ToList();
			if (unmapped.Count > 0)
			{
				Console.WriteLine();
				Console.WriteLine("Unmapped columns (normal - most have no PA equivalent):");
				Console.WriteLine("  " + string.Join(", ", unmapped));
			}

			if (!map.HasPhonetic)
			{
				Console.Error.WriteLine();
				Console.Error.WriteLine(
					"No column auto-mapped to Phonetic - cannot convert. Columns seen: " +
					string.Join(", ", db.Columns));
				return 1;
			}

			var result = SfmWriter.Write(db, map, output);

			Console.WriteLine();
			Console.WriteLine("Wrote " + result.RecordsWritten + " record(s) (" +
				result.RecordsSkippedNoPhonetic + " skipped, no phonetic) to:");
			Console.WriteLine("  " + output);
			Console.WriteLine();
			Console.WriteLine("In PA: Add > Non-FieldWorks Data Source > Toolbox Files (*.db).");
			Console.WriteLine("Note: this is a SNAPSHOT - re-run after editing in Dekereke.");
			return 0;
		}
	}
}
