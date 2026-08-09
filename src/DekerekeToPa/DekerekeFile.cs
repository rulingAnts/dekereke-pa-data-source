using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace DekerekeToPa
{
	/// <summary>
	/// One Dekereke record (one &lt;data_form&gt; element): column name -> value.
	/// </summary>
	public sealed class DekerekeRecord
	{
		private readonly Dictionary<string, string> _values =
			new Dictionary<string, string>(StringComparer.Ordinal);

		public IReadOnlyDictionary<string, string> Values
		{
			get { return _values; }
		}

		/// <summary>Returns the value for a column, or null when absent. Never throws.</summary>
		public string Get(string column)
		{
			if (column == null)
				return null;
			string v;
			return _values.TryGetValue(column, out v) ? v : null;
		}

		internal void Set(string column, string value)
		{
			_values[column] = value;
		}
	}

	/// <summary>
	/// An in-memory Dekereke database: the union of column names (in first-seen
	/// order) plus all records.
	/// </summary>
	public sealed class DekerekeDatabase
	{
		public DekerekeDatabase(string sourcePath)
		{
			SourcePath = sourcePath;
			Columns = new List<string>();
			Records = new List<DekerekeRecord>();
		}

		public string SourcePath { get; private set; }

		/// <summary>Union of column (child element) names across all records, first-seen order.</summary>
		public List<string> Columns { get; private set; }

		public List<DekerekeRecord> Records { get; private set; }

		/// <summary>
		/// The audio folder from the sibling *-DkUserSettings.xml (&lt;sound_file_path&gt;),
		/// or null when that file or element is absent. Dekereke stores bare .wav names
		/// in the database; the folder lives in the user-settings file.
		/// </summary>
		public string SoundFilePath { get; set; }
	}

	/// <summary>
	/// Reads Dekereke database XML.
	///
	/// ENCODING - the one real trap in this whole project:
	/// current Dekereke writes UTF-16LE with BOM (declaration says encoding="utf-16");
	/// newer versions write UTF-8. Every reader here therefore opens the RAW STREAM and
	/// hands it to XmlReader/XDocument, which sniff the BOM and declaration themselves.
	/// Never File.ReadAllText first: a string already decoded from UTF-16 still carries
	/// encoding="utf-16" in its declaration, and XmlReader then throws
	/// "There is no Unicode byte order mark."
	/// </summary>
	public static class DekerekeFile
	{
		public const string RootElementName = "phon_data";
		public const string RecordElementName = "data_form";

		/// <summary>
		/// Cheap check that a file is a Dekereke database: well-formed XML whose root
		/// element is &lt;phon_data&gt;. Reads only up to the root start tag.
		/// Returns false (never throws) for missing files, non-XML, or foreign XML.
		/// </summary>
		public static bool Sniff(string path)
		{
			try
			{
				if (!File.Exists(path))
					return false;

				var settings = new XmlReaderSettings
				{
					DtdProcessing = DtdProcessing.Ignore,
					IgnoreWhitespace = true,
					IgnoreComments = true,
					IgnoreProcessingInstructions = true
				};

				using (var stream = File.OpenRead(path))
				using (var reader = XmlReader.Create(stream, settings))
				{
					reader.MoveToContent();
					return reader.NodeType == XmlNodeType.Element &&
						reader.LocalName == RootElementName;
				}
			}
			catch
			{
				return false;
			}
		}

		/// <summary>
		/// Loads the full database. Columns are the union of child element names across
		/// all records, in first-seen order. Nested structures (e.g. Dekereke's
		/// &lt;qvp_acoustic_data_&gt; acoustic measurements) are skipped: a column is,
		/// by definition, an element with text-only content.
		/// Also picks up the audio folder from a sibling *-DkUserSettings.xml if present.
		/// </summary>
		public static DekerekeDatabase Read(string path)
		{
			var db = new DekerekeDatabase(path);
			var seen = new HashSet<string>(StringComparer.Ordinal);

			using (var stream = File.OpenRead(path))
			{
				var doc = XDocument.Load(stream);
				var root = doc.Root;
				if (root == null || root.Name.LocalName != RootElementName)
					throw new InvalidDataException(
						string.Format("Not a Dekereke database (root element is not <{0}>): {1}",
							RootElementName, path));

				foreach (var form in root.Elements(RecordElementName))
				{
					var rec = new DekerekeRecord();
					foreach (var el in form.Elements())
					{
						if (el.HasElements)
							continue; // qvp_acoustic_data_ and any other nested block

						var name = el.Name.LocalName;
						rec.Set(name, el.Value ?? string.Empty);

						if (seen.Add(name))
							db.Columns.Add(name);
					}
					db.Records.Add(rec);
				}
			}

			db.SoundFilePath = TryReadSoundFilePath(path);
			return db;
		}

		/// <summary>
		/// Dekereke keeps per-user settings in "&lt;database basename&gt;-DkUserSettings.xml"
		/// next to the database. We only want &lt;sound_file_path&gt;. Returns null when the
		/// file, the element, or its value is absent. Never throws.
		/// </summary>
		public static string TryReadSoundFilePath(string databasePath)
		{
			try
			{
				var dir = Path.GetDirectoryName(databasePath);
				var baseName = Path.GetFileNameWithoutExtension(databasePath);
				if (dir == null || baseName == null)
					return null;

				var settingsPath = Path.Combine(dir, baseName + "-DkUserSettings.xml");
				if (!File.Exists(settingsPath))
					return null;

				using (var stream = File.OpenRead(settingsPath))
				{
					var doc = XDocument.Load(stream);
					var el = doc.Root == null ? null : doc.Root.Element("sound_file_path");
					var value = el == null ? null : el.Value.Trim();
					return string.IsNullOrEmpty(value) ? null : value;
				}
			}
			catch
			{
				return null;
			}
		}
	}
}
