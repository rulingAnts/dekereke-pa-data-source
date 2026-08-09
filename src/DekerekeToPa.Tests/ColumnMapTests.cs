using System.IO;
using NUnit.Framework;

namespace DekerekeToPa.Tests
{
	[TestFixture]
	public class ColumnMapTests
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
		public void MappingStore_RoundTrips()
		{
			var store = new MappingStore();
			var map = new ColumnMap();
			map.Mappings.Add(new ColumnMapping("Phonetic", PaFieldNames.Phonetic));
			map.Mappings.Add(new ColumnMapping("Pitch", PaFieldNames.Tone));
			store.SetFor("C:\\data\\Fayu_stable.xml", map);

			var path = Path.Combine(_dir, "proj.DekerekeMappings.xml");
			store.Save(path);

			var loaded = MappingStore.Load(path);
			var found = loaded.FindFor("c:\\data\\fayu_stable.xml"); // case-insensitive

			Assert.That(found, Is.Not.Null);
			Assert.That(found.ColumnFor(PaFieldNames.Tone), Is.EqualTo("Pitch"));
			Assert.That(found.Mappings.Count, Is.EqualTo(2));
		}

		[Test]
		public void MappingStore_MissingFile_ReturnsEmptyStore()
		{
			var store = MappingStore.Load(Path.Combine(_dir, "absent.xml"));
			Assert.That(store, Is.Not.Null);
			Assert.That(store.Sources, Is.Empty);
		}

		[Test]
		public void MappingStore_CorruptFile_ReturnsEmptyStore()
		{
			var path = Path.Combine(_dir, "corrupt.xml");
			File.WriteAllText(path, "not xml at all");
			var store = MappingStore.Load(path);
			Assert.That(store.Sources, Is.Empty);
		}

		[Test]
		public void MappingStore_SetFor_ReplacesExistingEntry()
		{
			var store = new MappingStore();
			var map1 = new ColumnMap();
			map1.Mappings.Add(new ColumnMapping("Phonetic", PaFieldNames.Phonetic));
			store.SetFor("C:\\a.xml", map1);

			var map2 = new ColumnMap();
			map2.Mappings.Add(new ColumnMapping("Fonetik", PaFieldNames.Phonetic));
			store.SetFor("C:\\A.XML", map2); // same path, different case

			Assert.That(store.Sources.Count, Is.EqualTo(1));
			Assert.That(store.FindFor("C:\\a.xml").ColumnFor(PaFieldNames.Phonetic),
				Is.EqualTo("Fonetik"));
		}

		[Test]
		public void Validate_DuplicatePaField_Throws()
		{
			var map = new ColumnMap();
			map.Mappings.Add(new ColumnMapping("Phonetic", PaFieldNames.Phonetic));
			map.Mappings.Add(new ColumnMapping("Fonetik", PaFieldNames.Phonetic));
			Assert.That(() => map.Validate(), Throws.InvalidOperationException);
		}

		[Test]
		public void Validate_UnknownPaField_Throws()
		{
			var map = new ColumnMap();
			map.Mappings.Add(new ColumnMapping("Phonetic", PaFieldNames.Phonetic));
			map.Mappings.Add(new ColumnMapping("X", "NoSuchField"));
			Assert.That(() => map.Validate(), Throws.InvalidOperationException);
		}
	}
}
