// Dekereke Data Sources for Phonology Assistant
// Copyright (C) 2026 Seth Johnston
//
// SPDX-License-Identifier: AGPL-3.0-or-later OR MIT
//
// This file is part of the DekerekeToPa core library, which is DUAL-LICENSED:
// use it under the GNU Affero General Public License v3.0 or later (LICENSE),
// OR under the MIT License (LICENSE-MIT), at your option. The dual licence
// exists so this library can be contributed to MIT-licensed upstream projects
// such as Phonology Assistant. Everything else in this repository remains
// AGPL-3.0-or-later only. See LICENSING.md for the reasoning.
//
// This program is distributed in the hope that it will be useful, but WITHOUT
// ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or
// FITNESS FOR A PARTICULAR PURPOSE.

using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace DekerekeToPa
{
	public sealed class SfmWriteResult
	{
		public int RecordsWritten;
		public int RecordsSkippedNoPhonetic;
		public int TotalLines;
		public string OutputPath;
	}

	/// <summary>
	/// Emits a Toolbox-flavored standard-format file that Phonology Assistant reads
	/// natively (its most mature importer).
	///
	/// Constraints, all verified against PA's SfmDataSourceReader.cs:
	///  - PA reads the file with File.ReadAllLines; a line not starting with '\'
	///    is silently discarded, so values must never contain raw newlines
	///    (Flatten() collapses them to single spaces).
	///  - Lines within a record go into a Dictionary keyed by marker, so a marker
	///    must not repeat within a record (ColumnMap.Validate guarantees one
	///    column per PA field).
	///  - A record-marker line with an EMPTY value ("\ref" alone) fails PA's
	///    split-in-two parse and is discarded, which would silently merge two
	///    records - so an empty/unmapped Reference gets a synthesized number.
	///  - The "\_sh v3.0  400  PhoneticData" header makes PA type the file
	///    Toolbox; "\ref" matches PA's DefaultSfmRecordMarker setting, so the
	///    record marker is picked up with no user interaction.
	///  - Encoding is UTF-8 with BOM (matches PA's shipped Sekpele sample data).
	/// </summary>
	public static class SfmWriter
	{
		public const string ShoeboxHeader = "\\_sh v3.0  400  PhoneticData";
		private const string Crlf = "\r\n";

		public static SfmWriteResult Write(DekerekeDatabase db, ColumnMap map, string outputPath)
		{
			map.Validate();

			var phoneticCol = map.ColumnFor(PaFieldNames.Phonetic);
			var referenceCol = map.ColumnFor(PaFieldNames.Reference);

			var sb = new StringBuilder();
			sb.Append(ShoeboxHeader).Append(Crlf).Append(Crlf);

			var result = new SfmWriteResult { OutputPath = outputPath };
			int recordNumber = 0;

			foreach (var rec in db.Records)
			{
				recordNumber++;

				var phonetic = Flatten(rec.Get(phoneticCol));
				if (string.IsNullOrEmpty(phonetic))
				{
					result.RecordsSkippedNoPhonetic++;
					continue;
				}

				var reference = referenceCol == null ? null : Flatten(rec.Get(referenceCol));
				if (string.IsNullOrEmpty(reference))
					reference = recordNumber.ToString("D4");

				sb.Append("\\ref ").Append(reference).Append(Crlf);

				foreach (var m in map.Mappings)
				{
					if (m.PaField == PaFieldNames.Reference)
						continue; // written first, above

					var value = Flatten(rec.Get(m.Column));
					if (string.IsNullOrEmpty(value))
						continue;

					if (m.PaField == PaFieldNames.AudioFile)
						value = ResolveAudioPath(db.SoundFilePath, value);

					sb.Append(PaFieldNames.SfmMarkers[m.PaField]).Append(' ')
					  .Append(value).Append(Crlf);
				}

				sb.Append(Crlf);
				result.RecordsWritten++;
			}

			var dir = Path.GetDirectoryName(outputPath);
			if (!string.IsNullOrEmpty(dir))
				Directory.CreateDirectory(dir);

			var text = sb.ToString();
			File.WriteAllText(outputPath, text, new UTF8Encoding(true));

			result.TotalLines = CountLines(text);
			return result;
		}

		/// <summary>
		/// Collapses newlines/tabs/space runs to a single space and trims.
		/// Deliberately does NOT use \s+ - .NET's \s matches NBSP and other
		/// Unicode space separators, which may be meaningful inside phonetic data.
		/// </summary>
		public static string Flatten(string value)
		{
			if (value == null)
				return null;
			return Regex.Replace(value, "[\\r\\n\\t ]+", " ").Trim();
		}

		/// <summary>
		/// Dekereke stores bare sound file names; the folder lives in the user
		/// settings file. Prefix when we know the folder and the value is not
		/// already a path. Backslash join: the output is consumed on Windows.
		/// </summary>
		public static string ResolveAudioPath(string soundFolder, string fileName)
		{
			if (string.IsNullOrEmpty(soundFolder))
				return fileName;

			if (fileName.IndexOf('\\') >= 0 || fileName.IndexOf('/') >= 0 ||
				fileName.IndexOf(':') >= 0)
			{
				return fileName; // already a path of some kind
			}

			return soundFolder.TrimEnd('\\', '/') + "\\" + fileName;
		}

		private static int CountLines(string text)
		{
			int count = 1;
			for (int i = 0; i + 1 < text.Length; i++)
			{
				if (text[i] == '\r' && text[i + 1] == '\n')
					count++;
			}
			return count;
		}
	}
}
