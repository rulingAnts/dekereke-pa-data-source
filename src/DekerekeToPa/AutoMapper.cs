using System;
using System.Collections.Generic;
using System.Linq;

namespace DekerekeToPa
{
	/// <summary>
	/// Guesses a ColumnMap from a database's actual column names.
	///
	/// Dekereke column names are user-defined per database, so no fixed mapping can
	/// work; instead each PA field carries an ordered synonym list (English and
	/// Indonesian - Dekereke is widely used in Indonesian-language projects).
	/// For each PA field, in order, the first not-yet-claimed column matching a
	/// synonym (case-insensitive) wins; each column is claimed at most once.
	/// Unmatched columns stay unmapped - that is normal, most Dekereke columns
	/// (elicitation frames, per-paradigm pitch columns, ...) have no PA equivalent.
	/// </summary>
	public static class AutoMapper
	{
		// Order matters twice: fields earlier in this list claim columns first, and
		// within a field the earlier synonym wins (e.g. a database with both
		// "Orthography" and "Tulisan" maps Orthography, leaving Tulisan unmapped).
		private static readonly KeyValuePair<string, string[]>[] Synonyms =
		{
			new KeyValuePair<string, string[]>(PaFieldNames.Phonetic,
				new[] { "Phonetic", "Fonetik", "IPA" }),
			new KeyValuePair<string, string[]>(PaFieldNames.Reference,
				new[] { "Reference", "Ref", "No", "Nomor" }),
			new KeyValuePair<string, string[]>(PaFieldNames.Tone,
				new[] { "Pitch", "Tone", "Nada", "Surface_Melody" }),
			new KeyValuePair<string, string[]>(PaFieldNames.Phonemic,
				new[] { "Phonemic", "Fonemik" }),
			new KeyValuePair<string, string[]>(PaFieldNames.Gloss,
				new[] { "Gloss", "Arti", "EnglishGloss" }),
			new KeyValuePair<string, string[]>(PaFieldNames.GlossSecondary,
				new[] { "IndonesianGloss", "Gloss2", "ArtiIndonesia", "NationalGloss" }),
			new KeyValuePair<string, string[]>(PaFieldNames.PartOfSpeech,
				new[] { "Category", "POS", "PartOfSpeech", "Kategori", "KelasKata" }),
			new KeyValuePair<string, string[]>(PaFieldNames.Orthographic,
				new[] { "Orthography", "Tulisan", "Ejaan" }),
			new KeyValuePair<string, string[]>(PaFieldNames.AudioFile,
				new[] { "SoundFile", "Audio", "Sound", "Rekaman" }),
			new KeyValuePair<string, string[]>(PaFieldNames.Note,
				new[] { "Notes", "Note", "Catatan" })
		};

		public static ColumnMap Map(IEnumerable<string> columns)
		{
			var available = columns.ToList();
			var claimed = new HashSet<string>(StringComparer.Ordinal);
			var result = new ColumnMap();

			foreach (var field in Synonyms)
			{
				string match = null;

				foreach (var synonym in field.Value)
				{
					match = available.FirstOrDefault(c =>
						!claimed.Contains(c) &&
						string.Equals(c, synonym, StringComparison.OrdinalIgnoreCase));

					if (match != null)
						break;
				}

				if (match == null)
					continue;

				claimed.Add(match);
				result.Mappings.Add(new ColumnMapping(match, field.Key));
			}

			return result;
		}

		/// <summary>
		/// Re-runs the heuristics over columns that appeared since the map was saved
		/// (the user added columns in Dekereke). Existing choices are never changed;
		/// only new columns can gain mappings, and only onto still-unclaimed PA fields.
		/// This runs silently on every load - it must never prompt.
		/// </summary>
		public static bool MapNewColumns(ColumnMap existing, IEnumerable<string> columns)
		{
			var knownColumns = new HashSet<string>(
				existing.Mappings.Select(m => m.Column), StringComparer.Ordinal);
			var usedFields = new HashSet<string>(
				existing.Mappings.Select(m => m.PaField), StringComparer.Ordinal);

			var fresh = Map(columns);
			var changed = false;

			foreach (var m in fresh.Mappings)
			{
				if (knownColumns.Contains(m.Column) || usedFields.Contains(m.PaField))
					continue;

				existing.Mappings.Add(m);
				knownColumns.Add(m.Column);
				usedFields.Add(m.PaField);
				changed = true;
			}

			return changed;
		}
	}
}
