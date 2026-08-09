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
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace DekerekeToPa.Tests
{
	[TestFixture]
	public class SfmWriterTests
	{
		private string _dir;
		private DekerekeDatabase _db;
		private ColumnMap _map;
		private string _outPath;

		[SetUp]
		public void SetUp()
		{
			_dir = Fixtures.NewTempDir();
			_db = DekerekeFile.Read(Fixtures.WriteUtf16(_dir));
			_map = AutoMapper.Map(_db.Columns);
			_outPath = Path.Combine(_dir, "out", "Fayu_test.xml");
		}

		[TearDown]
		public void TearDown()
		{
			try { Directory.Delete(_dir, true); } catch { /* best effort */ }
		}

		private string[] WriteAndReadLines()
		{
			SfmWriter.Write(_db, _map, _outPath);
			return File.ReadAllLines(_outPath);
		}

		[Test]
		public void Write_StartsWithShoeboxHeader()
		{
			var lines = WriteAndReadLines();
			Assert.That(lines[0], Is.EqualTo(SfmWriter.ShoeboxHeader));
		}

		[Test]
		public void Write_HasUtf8Bom()
		{
			SfmWriter.Write(_db, _map, _outPath);
			var bytes = File.ReadAllBytes(_outPath);
			Assert.That(bytes.Length, Is.GreaterThan(3));
			Assert.That(bytes[0], Is.EqualTo(0xEF));
			Assert.That(bytes[1], Is.EqualTo(0xBB));
			Assert.That(bytes[2], Is.EqualTo(0xBF));
		}

		[Test]
		public void Write_SkipsRecordsWithoutPhonetic()
		{
			var result = SfmWriter.Write(_db, _map, _outPath);

			// Fixture: 4 records, one with empty Phonetic.
			Assert.That(result.RecordsWritten, Is.EqualTo(3));
			Assert.That(result.RecordsSkippedNoPhonetic, Is.EqualTo(1));

			var text = File.ReadAllText(_outPath);
			Assert.That(text, Does.Not.Contain("descend")); // the skipped record
		}

		[Test]
		public void Write_OneRefPerWrittenRecord_AndRefIsFirst()
		{
			var lines = WriteAndReadLines();
			var refLines = lines.Where(l => l.StartsWith("\\ref ")).ToList();

			Assert.That(refLines.Count, Is.EqualTo(3));

			// Every record block must open with \ref (PA's reader discards
			// everything before the first record-marker line it recognizes).
			var firstMarkerLine = lines.First(l => l.StartsWith("\\") && !l.StartsWith("\\_sh"));
			Assert.That(firstMarkerLine, Does.StartWith("\\ref "));
		}

		[Test]
		public void Write_FlattensNewlinesInValues()
		{
			var lines = WriteAndReadLines();

			// The canoe record's Notes contained a raw newline.
			var noteLine = lines.Single(l => l.StartsWith("\\nt "));
			Assert.That(noteLine, Is.EqualTo("\\nt elicited twice; second time with frame"));

			// And nothing outside a marker line survives (PA would discard it).
			foreach (var line in lines.Where(l => l.Length > 0))
				Assert.That(line[0], Is.EqualTo('\\'), "non-marker line leaked: " + line);
		}

		[Test]
		public void Write_SynthesizesReferenceWhenEmpty()
		{
			var lines = WriteAndReadLines();

			// The canoe record (4th in file) has an empty <Reference>; it must get
			// a synthesized number, not an empty \ref line.
			var canoeRefIndex = lines.ToList().FindIndex(l => l == "\\ge canoe") ;
			Assert.That(canoeRefIndex, Is.GreaterThan(0));

			var block = lines.Take(canoeRefIndex).ToList();
			var lastRef = block.Last(l => l.StartsWith("\\ref "));
			Assert.That(lastRef.Substring(5).Trim(), Is.Not.Empty);
			Assert.That(lastRef, Is.EqualTo("\\ref 0004"));
		}

		[Test]
		public void Write_NoDuplicateMarkerWithinARecord()
		{
			var lines = WriteAndReadLines();

			var current = new System.Collections.Generic.List<string>();
			foreach (var line in lines.Skip(1)) // skip \_sh
			{
				if (line.StartsWith("\\ref "))
					current.Clear();

				if (line.Length == 0)
					continue;

				var marker = line.Split(' ')[0];
				Assert.That(current, Does.Not.Contain(marker),
					"duplicate marker in one record: " + marker);
				current.Add(marker);
			}
		}

		[Test]
		public void Write_PrefixesAudioWithSoundFolder()
		{
			_db.SoundFilePath = "\\\\Mac\\GIT\\dekereke-sync\\audio";
			var lines = WriteAndReadLines();

			var sfLine = lines.First(l => l.StartsWith("\\sf "));
			Assert.That(sfLine,
				Is.EqualTo("\\sf \\\\Mac\\GIT\\dekereke-sync\\audio\\0015_swamp.wav"));
		}

		[Test]
		public void Write_LeavesAudioAloneWithoutSoundFolder()
		{
			Assert.That(_db.SoundFilePath, Is.Null);
			var lines = WriteAndReadLines();

			var sfLine = lines.First(l => l.StartsWith("\\sf "));
			Assert.That(sfLine, Is.EqualTo("\\sf 0015_swamp.wav"));
		}

		/// <summary>
		/// Source encoding must not leak into the output: an old UTF-16LE database
		/// and a current plain-UTF-8 one convert to the same bytes.
		/// </summary>
		[Test]
		public void Write_AllSourceEncodings_ProduceIdenticalOutput()
		{
			SfmWriter.Write(_db, _map, _outPath);
			var baseline = File.ReadAllBytes(_outPath);

			var variants = new[]
			{
				Fixtures.WriteUtf8(_dir),
				Fixtures.WriteUtf8NoBom(_dir),
				Fixtures.WriteUtf8NoBomNoDecl(_dir)
			};

			int n = 0;
			foreach (var source in variants)
			{
				var db = DekerekeFile.Read(source);
				var outPath = Path.Combine(_dir, "out", "variant" + (n++) + ".xml");
				SfmWriter.Write(db, AutoMapper.Map(db.Columns), outPath);

				Assert.That(File.ReadAllBytes(outPath), Is.EqualTo(baseline), source);
			}
		}

		[Test]
		public void Write_TotalLinesMatchesFile()
		{
			var result = SfmWriter.Write(_db, _map, _outPath);
			var lines = File.ReadAllLines(_outPath);
			Assert.That(result.TotalLines, Is.EqualTo(lines.Length).Within(1));
		}

		[Test]
		public void Write_PhoneticSurvivesRoundTrip()
		{
			var lines = WriteAndReadLines();
			var phLines = lines.Where(l => l.StartsWith("\\ph ")).Select(l => l.Substring(4)).ToList();

			Assert.That(phLines, Does.Contain("tei"));
			Assert.That(phLines, Does.Contain("ku\u026Di"));   // U+026D retroflex l
			Assert.That(phLines, Does.Contain("\u0278wu"));    // U+0278 bilabial fricative
		}
	}
}
