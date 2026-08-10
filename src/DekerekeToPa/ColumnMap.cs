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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace DekerekeToPa
{
	/// <summary>One Dekereke column mapped to one PA field.</summary>
	public class ColumnMapping
	{
		[XmlAttribute("column")]
		public string Column;

		[XmlAttribute("paField")]
		public string PaField;

		public ColumnMapping()
		{
		}

		public ColumnMapping(string column, string paField)
		{
			Column = column;
			PaField = paField;
		}
	}

	/// <summary>
	/// The mapping for one Dekereke database: which of its columns feed which PA fields.
	/// At most one column per PA field (SFM markers may not repeat within a record -
	/// PA's SfmDataSourceReader keys lines into a Dictionary, so a repeat silently
	/// loses data). Phonetic is required; everything else optional.
	/// </summary>
	[XmlType("dekerekeColumnMap")]
	public class ColumnMap
	{
		[XmlElement("map")]
		public List<ColumnMapping> Mappings = new List<ColumnMapping>();

		/// <summary>The Dekereke column mapped to the given PA field, or null.</summary>
		public string ColumnFor(string paField)
		{
			var m = Mappings.FirstOrDefault(x => x.PaField == paField);
			return m == null ? null : m.Column;
		}

		public bool HasPhonetic
		{
			get { return ColumnFor(PaFieldNames.Phonetic) != null; }
		}

		/// <summary>
		/// Throws with a clear message when the map is unusable: no phonetic column,
		/// a PA field claimed twice, a column claimed twice, or an unknown PA field.
		/// </summary>
		public void Validate()
		{
			if (!HasPhonetic)
				throw new InvalidOperationException(
					"No Dekereke column is mapped to the Phonetic field. " +
					"Phonology Assistant cannot use a data source without phonetic data.");

			var dupField = Mappings.GroupBy(m => m.PaField).FirstOrDefault(g => g.Count() > 1);
			if (dupField != null)
				throw new InvalidOperationException(
					string.Format("PA field '{0}' is mapped from more than one Dekereke column.", dupField.Key));

			var dupCol = Mappings.GroupBy(m => m.Column).FirstOrDefault(g => g.Count() > 1);
			if (dupCol != null)
				throw new InvalidOperationException(
					string.Format("Dekereke column '{0}' is mapped more than once.", dupCol.Key));

			var unknown = Mappings.FirstOrDefault(m => !PaFieldNames.SfmMarkers.ContainsKey(m.PaField));
			if (unknown != null)
				throw new InvalidOperationException(
					string.Format("Unknown PA field '{0}'.", unknown.PaField));
		}
	}

	/// <summary>One saved mapping, keyed by the Dekereke file it belongs to.</summary>
	public class SourceMapEntry
	{
		[XmlAttribute("sourcePath")]
		public string SourcePath;

		[XmlElement("columns")]
		public ColumnMap Map;
	}

	/// <summary>
	/// All Dekereke mappings for one PA project. The add-on stores this as
	/// "&lt;project name&gt;.DekerekeMappings.xml" in the PA project folder - written and
	/// read only by this tool; the user never edits it.
	/// </summary>
	[XmlRoot("dekerekeMappings")]
	public class MappingStore
	{
		[XmlElement("source")]
		public List<SourceMapEntry> Sources = new List<SourceMapEntry>();

		/// <summary>Case-insensitive on path, since this is a Windows-hosted app.</summary>
		public ColumnMap FindFor(string sourcePath)
		{
			var entry = Sources.FirstOrDefault(s =>
				string.Equals(s.SourcePath, sourcePath, StringComparison.OrdinalIgnoreCase));
			return entry == null ? null : entry.Map;
		}

		public void SetFor(string sourcePath, ColumnMap map)
		{
			var entry = Sources.FirstOrDefault(s =>
				string.Equals(s.SourcePath, sourcePath, StringComparison.OrdinalIgnoreCase));

			if (entry == null)
				Sources.Add(new SourceMapEntry { SourcePath = sourcePath, Map = map });
			else
				entry.Map = map;
		}

		public static MappingStore Load(string path)
		{
			if (!File.Exists(path))
				return new MappingStore();

			try
			{
				var serializer = new XmlSerializer(typeof(MappingStore));
				using (var stream = File.OpenRead(path))
					return (MappingStore)serializer.Deserialize(stream);
			}
			catch
			{
				// A corrupt store is not fatal - the auto-mapper will rebuild it.
				return new MappingStore();
			}
		}

		public void Save(string path)
		{
			var dir = Path.GetDirectoryName(path);
			if (!string.IsNullOrEmpty(dir))
				Directory.CreateDirectory(dir);

			var serializer = new XmlSerializer(typeof(MappingStore));
			using (var stream = File.Create(path))
				serializer.Serialize(stream, this);
		}
	}
}
