using System.IO;
using System.Text;
using NUnit.Framework;

namespace DekerekeToPa.Tests
{
	[TestFixture]
	public class DekerekeFileTests
	{
		private string _dir;

		[SetUp]
		public void SetUp()
		{
			_dir = Fixtures.NewTempDir();
		}

		[TearDown]
		public void TearDown()
		{
			try { Directory.Delete(_dir, true); } catch { /* best effort */ }
		}

		[Test]
		public void Sniff_Utf16File_IsTrue()
		{
			var path = Fixtures.WriteUtf16(_dir);
			Assert.That(DekerekeFile.Sniff(path), Is.True);
		}

		[Test]
		public void Sniff_Utf8File_IsTrue()
		{
			var path = Fixtures.WriteUtf8(_dir);
			Assert.That(DekerekeFile.Sniff(path), Is.True);
		}

		[Test]
		public void Sniff_ForeignXml_IsFalse()
		{
			var path = Path.Combine(_dir, "other.xml");
			File.WriteAllText(path, "<?xml version=\"1.0\"?><lift producer=\"x\"/>", new UTF8Encoding(true));
			Assert.That(DekerekeFile.Sniff(path), Is.False);
		}

		[Test]
		public void Sniff_NonXml_IsFalse()
		{
			var path = Path.Combine(_dir, "notes.db");
			File.WriteAllText(path, "\\_sh v3.0  400  PhoneticData\r\n\r\n\\ref 0001\r\n");
			Assert.That(DekerekeFile.Sniff(path), Is.False);
		}

		[Test]
		public void Sniff_MissingFile_IsFalse()
		{
			Assert.That(DekerekeFile.Sniff(Path.Combine(_dir, "no-such.xml")), Is.False);
		}

		[Test]
		public void Read_ColumnsAreUnionInFirstSeenOrder()
		{
			var db = DekerekeFile.Read(Fixtures.WriteUtf16(_dir));

			// Phonemic/Orthography only appear in record 2, Notes only in record 4 -
			// all must still be present, after the record-1 columns.
			Assert.That(db.Columns, Does.Contain("Phonemic"));
			Assert.That(db.Columns, Does.Contain("Orthography"));
			Assert.That(db.Columns, Does.Contain("Notes"));
			Assert.That(db.Columns.IndexOf("Reference"), Is.EqualTo(0));
			Assert.That(db.Columns.IndexOf("Phonemic"),
				Is.GreaterThan(db.Columns.IndexOf("SoundFile")));
		}

		[Test]
		public void Read_SkipsNestedAcousticData()
		{
			var db = DekerekeFile.Read(Fixtures.WriteUtf16(_dir));

			Assert.That(db.Columns, Does.Not.Contain("qvp_acoustic_data_"));
			Assert.That(db.Columns, Does.Not.Contain("qvp_column"));
			Assert.That(db.Records[0].Get("qvp_acoustic_data_"), Is.Null);
		}

		[Test]
		public void Read_Utf16AndUtf8_YieldIdenticalContent()
		{
			var db16 = DekerekeFile.Read(Fixtures.WriteUtf16(_dir));
			var db8 = DekerekeFile.Read(Fixtures.WriteUtf8(_dir));

			Assert.That(db8.Records.Count, Is.EqualTo(db16.Records.Count));
			Assert.That(db8.Columns, Is.EqualTo(db16.Columns));
			Assert.That(db8.Records[1].Get("Phonetic"), Is.EqualTo(db16.Records[1].Get("Phonetic")));
			// "ku\u026Di" spelled with an escape so a Windows system-codepage
			// compiler can never mangle the source literal.
			Assert.That(db16.Records[1].Get("Phonetic"), Is.EqualTo("ku\u026Di"));
		}

		[Test]
		public void Read_EmptyElementsAreEmptyStrings()
		{
			var db = DekerekeFile.Read(Fixtures.WriteUtf16(_dir));

			Assert.That(db.Records[0].Get("Type"), Is.EqualTo(string.Empty));
			Assert.That(db.Records[2].Get("Phonetic"), Is.EqualTo(string.Empty));
		}

		[Test]
		public void Read_PicksUpSoundFilePathFromUserSettings()
		{
			var dbPath = Fixtures.WriteUtf16(_dir);
			Fixtures.WriteUserSettings(_dir, dbPath, "\\\\Mac\\GIT\\dekereke-sync\\audio");

			var db = DekerekeFile.Read(dbPath);
			Assert.That(db.SoundFilePath, Is.EqualTo("\\\\Mac\\GIT\\dekereke-sync\\audio"));
		}

		[Test]
		public void Read_NoUserSettings_SoundFilePathIsNull()
		{
			var db = DekerekeFile.Read(Fixtures.WriteUtf16(_dir));
			Assert.That(db.SoundFilePath, Is.Null);
		}

		/// <summary>
		/// Smoke test against a real full-size database when one is available
		/// (developer machines only; CI has no such file and skips).
		/// </summary>
		[Test]
		public void Read_RealFayuDatabase_WhenPresent()
		{
			const string real = "/Users/Seth/GIT/dekereke-stable/Fayu_stable.xml";
			if (!File.Exists(real))
				Assert.Ignore("Real Fayu database not present on this machine.");

			Assert.That(DekerekeFile.Sniff(real), Is.True);
			var db = DekerekeFile.Read(real);
			Assert.That(db.Records.Count, Is.GreaterThan(100));
			Assert.That(db.Columns, Does.Contain("Phonetic"));
		}
	}
}
