using System.Collections.Generic;

namespace DekerekeToPa
{
	/// <summary>
	/// The Phonology Assistant fields this tool can map Dekereke columns onto,
	/// and the standard-format markers used for each when writing SFM.
	///
	/// Field names must match PA's DistFiles/Configuration/DefaultFields.xml exactly.
	/// Marker choices: where PA declares possibleDataSourceFieldNames for a field
	/// (Reference \ref, Phonetic \ph, Gloss \ge, Gloss-Secondary \gn,
	/// PartOfSpeech \ps, Tone \tn, AudioFile \sf) we use those markers, so the
	/// generated file still auto-maps if someone opens it in PA without the add-on.
	/// Phonemic, Orthographic and Note have no recognized markers in PA at all -
	/// \pm, \or and \nt are our own, and the add-on maps them programmatically.
	/// </summary>
	public static class PaFieldNames
	{
		public const string Reference = "Reference";
		public const string Phonetic = "Phonetic";
		public const string Tone = "Tone";
		public const string Phonemic = "Phonemic";
		public const string Gloss = "Gloss";
		public const string GlossSecondary = "Gloss-Secondary";
		public const string PartOfSpeech = "PartOfSpeech";
		public const string Orthographic = "Orthographic";
		public const string AudioFile = "AudioFile";
		public const string Note = "Note";

		/// <summary>All mappable PA fields, in the order they are written per SFM record.</summary>
		public static readonly IReadOnlyList<string> All = new[]
		{
			Reference,
			Phonetic,
			Tone,
			Phonemic,
			Gloss,
			GlossSecondary,
			PartOfSpeech,
			Orthographic,
			AudioFile,
			Note
		};

		/// <summary>SFM marker (including backslash) for each PA field.</summary>
		public static readonly IReadOnlyDictionary<string, string> SfmMarkers =
			new Dictionary<string, string>
			{
				{ Reference, "\\ref" },
				{ Phonetic, "\\ph" },
				{ Tone, "\\tn" },
				{ Phonemic, "\\pm" },
				{ Gloss, "\\ge" },
				{ GlossSecondary, "\\gn" },
				{ PartOfSpeech, "\\ps" },
				{ Orthographic, "\\or" },
				{ AudioFile, "\\sf" },
				{ Note, "\\nt" }
			};

		/// <summary>
		/// PA fields whose FieldMapping.IsParsed should be true, mirroring PA's
		/// application setting DefaultParsedSfmFields:
		/// "Phonetic;Phonemic;Gloss;Gloss-Secondary;Gloss-Other;PartOfSpeech;Tone;Orthographic".
		/// </summary>
		public static readonly ISet<string> ParsedByDefault = new HashSet<string>
		{
			Phonetic,
			Phonemic,
			Gloss,
			GlossSecondary,
			PartOfSpeech,
			Tone,
			Orthographic
		};
	}
}
