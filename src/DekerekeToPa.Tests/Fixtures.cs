using System;
using System.IO;
using System.Text;

namespace DekerekeToPa.Tests
{
	/// <summary>
	/// Builds Dekereke fixture files at run time in a temp folder.
	///
	/// Encodings are the whole point of several tests, so fixtures are written
	/// from code with explicit encodings rather than committed as files an editor
	/// or git filter could silently re-encode. Content is a trimmed-down version
	/// of real Fayu (Lakes Plain, Papua) records.
	/// </summary>
	public static class Fixtures
	{
		// Record 1: full record, incl. a nested acoustic block that must be skipped.
		// Record 2: introduces a column (Phonemic) absent from record 1 - tests
		//           that Columns is a union, not just the first record's shape.
		// Record 3: empty Phonetic - must be skipped by the SFM writer.
		// Record 4: newline inside Notes - must be flattened; empty Reference -
		//           must get a synthesized one.
		public const string Body =
"<phon_data>\n" +
"	<data_form>\n" +
"		<Reference>0015</Reference>\n" +
"		<Gloss>swamp</Gloss>\n" +
"		<IndonesianGloss>rawa</IndonesianGloss>\n" +
"		<Category>Noun</Category>\n" +
"		<Type />\n" +
"		<Phonetic>tei</Phonetic>\n" +
"		<Pitch>[K k]</Pitch>\n" +
"		<SoundFile>0015_swamp.wav</SoundFile>\n" +
"		<qvp_acoustic_data_>\n" +
"			<qvp_acoustic_data_set>\n" +
"				<qvp_column>Phonetic</qvp_column>\n" +
"				<qvp_acoustic_data_string>V1T:0.137</qvp_acoustic_data_string>\n" +
"			</qvp_acoustic_data_set>\n" +
"		</qvp_acoustic_data_>\n" +
"	</data_form>\n" +
"	<data_form>\n" +
"		<Reference>0021</Reference>\n" +
"		<Gloss>gecko</Gloss>\n" +
"		<IndonesianGloss>cicak</IndonesianGloss>\n" +
"		<Category>Noun</Category>\n" +
"		<Phonetic>ku\u026Di</Phonetic>\n" +
"		<Pitch>[3 K k]</Pitch>\n" +
"		<Phonemic>ku.di</Phonemic>\n" +
"		<Orthography>kudi</Orthography>\n" +
"		<SoundFile>0021_gecko.wav</SoundFile>\n" +
"	</data_form>\n" +
"	<data_form>\n" +
"		<Reference>0012</Reference>\n" +
"		<Gloss>descend.INCMP</Gloss>\n" +
"		<Category>DUPLICATE</Category>\n" +
"		<Phonetic></Phonetic>\n" +
"		<SoundFile>0012_descend.wav</SoundFile>\n" +
"	</data_form>\n" +
"	<data_form>\n" +
"		<Reference></Reference>\n" +
"		<Gloss>canoe</Gloss>\n" +
"		<Phonetic>\u0278wu</Phonetic>\n" +
"		<Notes>elicited twice;\n" +
"second time with frame</Notes>\n" +
"	</data_form>\n" +
"</phon_data>\n";

		/// <summary>
		/// UTF-16LE with BOM, declaration says utf-16 - byte-for-byte what current
		/// Dekereke writes. Encoding.Unicode is UTF-16LE and emits the BOM via
		/// File.WriteAllText.
		/// </summary>
		public static string WriteUtf16(string dir, string name = "Fayu_test.xml")
		{
			var path = Path.Combine(dir, name);
			var content = "<?xml version=\"1.0\" encoding=\"utf-16\"?>\n" + Body;
			File.WriteAllText(path, content, Encoding.Unicode);
			return path;
		}

		/// <summary>UTF-8 with BOM, what newer Dekereke versions write.</summary>
		public static string WriteUtf8(string dir, string name = "Fayu_test_utf8.xml")
		{
			var path = Path.Combine(dir, name);
			var content = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" + Body;
			File.WriteAllText(path, content, new UTF8Encoding(true));
			return path;
		}

		/// <summary>A sibling *-DkUserSettings.xml carrying the audio folder.</summary>
		public static string WriteUserSettings(string dir, string dbFileName, string soundPath)
		{
			var baseName = Path.GetFileNameWithoutExtension(dbFileName);
			var path = Path.Combine(dir, baseName + "-DkUserSettings.xml");
			var content =
				"<?xml version=\"1.0\" encoding=\"utf-16\" standalone=\"yes\"?>\n" +
				"<settings>\n" +
				"	<sound_file_path>" + soundPath + "</sound_file_path>\n" +
				"	<praat_path>C:\\Program Files\\Praat\\Praat.exe</praat_path>\n" +
				"</settings>\n";
			File.WriteAllText(path, content, Encoding.Unicode);
			return path;
		}

		public static string NewTempDir()
		{
			var dir = Path.Combine(Path.GetTempPath(), "DekerekeToPaTests", Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(dir);
			return dir;
		}
	}
}
