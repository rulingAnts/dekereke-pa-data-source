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

using System.Linq;
using NUnit.Framework;

namespace DekerekeToPa.Tests
{
	[TestFixture]
	public class AutoMapperTests
	{
		// Column sets from the two real databases this tool was built against.
		private static readonly string[] FayuColumns =
		{
			"Reference", "Gloss", "IndonesianGloss", "Category", "Type",
			"SyllableProfile", "Phonetic", "Pitch", "Notes", "SoundFile",
			"goodX", "whiteX", "Xbad", "Phonemic", "Orthography",
			"Surface_Melody", "Tulisan", "Nada"
		};

		private static readonly string[] BarnabasColumns =
		{
			"Reference", "Category", "SoundFile", "IndonesianGloss",
			"Phonetic", "Tulisan", "Catatan"
		};

		[Test]
		public void Map_FayuColumns_ProducesConfirmedMapping()
		{
			var map = AutoMapper.Map(FayuColumns);

			Assert.That(map.ColumnFor(PaFieldNames.Phonetic), Is.EqualTo("Phonetic"));
			Assert.That(map.ColumnFor(PaFieldNames.Reference), Is.EqualTo("Reference"));
			Assert.That(map.ColumnFor(PaFieldNames.Tone), Is.EqualTo("Pitch"));
			Assert.That(map.ColumnFor(PaFieldNames.Phonemic), Is.EqualTo("Phonemic"));
			Assert.That(map.ColumnFor(PaFieldNames.Gloss), Is.EqualTo("Gloss"));
			Assert.That(map.ColumnFor(PaFieldNames.GlossSecondary), Is.EqualTo("IndonesianGloss"));
			Assert.That(map.ColumnFor(PaFieldNames.PartOfSpeech), Is.EqualTo("Category"));
			Assert.That(map.ColumnFor(PaFieldNames.Orthographic), Is.EqualTo("Orthography"));
			Assert.That(map.ColumnFor(PaFieldNames.AudioFile), Is.EqualTo("SoundFile"));
			Assert.That(map.ColumnFor(PaFieldNames.Note), Is.EqualTo("Notes"));

			// Elicitation-frame columns must stay unmapped.
			Assert.That(map.Mappings.Select(m => m.Column), Does.Not.Contain("goodX"));
			Assert.That(map.Mappings.Select(m => m.Column), Does.Not.Contain("Xbad"));

			map.Validate(); // must not throw
		}

		[Test]
		public void Map_BarnabasColumns_UsesIndonesianSynonyms()
		{
			var map = AutoMapper.Map(BarnabasColumns);

			Assert.That(map.ColumnFor(PaFieldNames.Phonetic), Is.EqualTo("Phonetic"));
			// No "Orthography" column, so the Indonesian synonym takes over.
			Assert.That(map.ColumnFor(PaFieldNames.Orthographic), Is.EqualTo("Tulisan"));
			Assert.That(map.ColumnFor(PaFieldNames.Note), Is.EqualTo("Catatan"));
			Assert.That(map.ColumnFor(PaFieldNames.GlossSecondary), Is.EqualTo("IndonesianGloss"));

			map.Validate();
		}

		[Test]
		public void Map_OrthographyBeatsTulisan_WhenBothPresent()
		{
			var map = AutoMapper.Map(new[] { "Phonetic", "Tulisan", "Orthography" });
			Assert.That(map.ColumnFor(PaFieldNames.Orthographic), Is.EqualTo("Orthography"));
		}

		[Test]
		public void Map_PitchBeatsNada_AndNadaStaysUnclaimed()
		{
			var map = AutoMapper.Map(new[] { "Phonetic", "Pitch", "Nada" });

			Assert.That(map.ColumnFor(PaFieldNames.Tone), Is.EqualTo("Pitch"));
			Assert.That(map.Mappings.Select(m => m.Column), Does.Not.Contain("Nada"));
		}

		[Test]
		public void Map_NeverAssignsColumnTwice()
		{
			var map = AutoMapper.Map(FayuColumns);
			var columns = map.Mappings.Select(m => m.Column).ToList();
			Assert.That(columns.Distinct().Count(), Is.EqualTo(columns.Count));
		}

		[Test]
		public void Map_NoPhoneticColumn_MapValidateThrows()
		{
			var map = AutoMapper.Map(new[] { "Reference", "Gloss" });
			Assert.That(map.HasPhonetic, Is.False);
			Assert.That(() => map.Validate(), Throws.InvalidOperationException);
		}

		[Test]
		public void MapNewColumns_AddsOnlyNewMappings_NeverChangesExisting()
		{
			var map = AutoMapper.Map(new[] { "Phonetic", "Gloss" });
			var before = map.Mappings.Count;

			// User later added a Pitch column in Dekereke.
			var changed = AutoMapper.MapNewColumns(map, new[] { "Phonetic", "Gloss", "Pitch" });

			Assert.That(changed, Is.True);
			Assert.That(map.Mappings.Count, Is.EqualTo(before + 1));
			Assert.That(map.ColumnFor(PaFieldNames.Tone), Is.EqualTo("Pitch"));
			Assert.That(map.ColumnFor(PaFieldNames.Phonetic), Is.EqualTo("Phonetic"));
		}

		[Test]
		public void MapNewColumns_DoesNotStealFieldsAlreadyMapped()
		{
			var map = AutoMapper.Map(new[] { "Phonetic", "Nada" }); // Tone <- Nada
			Assert.That(map.ColumnFor(PaFieldNames.Tone), Is.EqualTo("Nada"));

			// A Pitch column appears later; Tone is taken, so nothing changes.
			var changed = AutoMapper.MapNewColumns(map, new[] { "Phonetic", "Nada", "Pitch" });

			Assert.That(changed, Is.False);
			Assert.That(map.ColumnFor(PaFieldNames.Tone), Is.EqualTo("Nada"));
		}
	}
}
