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
