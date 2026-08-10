# Upstream proposal — ready to paste

File at: **https://github.com/sillsdev/phonology-assistant/issues/new**

Everything between the rules below is the issue body, written in Seth's voice.
Suggested title:

> Feature: Dekereke phonology databases as a native data source (working add-on exists; PR offered)

Notes before posting, not part of the issue:

- PLAN.md's sequencing still applies: file this, wait for a maintainer nod,
  *then* open the PR. Recent human PRs there were merged in 5–6 days, so a
  response is realistic.
- PLAN.md suggests looping in Larry Hayashi, who originally pointed at this
  area — a cc/mention is Seth's call and his relationship, so the draft leaves
  names out.
- The "offered under MIT" line about the core library requires settling the
  question in LICENSING.md (dual-license the core library, or relicense at
  submission time — copyright holder's call).

---

Provided with a corpus of phonetic data, Phonology Assistant charts it and
helps a user discover the rules of sound in a language. **Dekereke** ([Rod
Casali's Phonology Database software](https://casali.canil.ca)) is where a
substantial community of field linguists — including many Indonesian-language
projects — keeps exactly that corpus. The two tools serve the same users at the
same desks, but PA cannot read Dekereke's XML, so those users work from
exported snapshots that go stale on every edit.

I'd like to contribute native support: `DataSourceType.Dekereke`, purely
additive, touching no existing behaviour. **This is not a speculative feature
request — it already works.** I built and shipped it as an add-on against
stock PA 4.1.1, using the `App.ReadAddOns()` loader:

- Working release, installer and demo video: https://rulingants.github.io/dekereke-pa-data-source/
- Source (including the PA-independent parsing/mapping core): https://github.com/rulingAnts/dekereke-pa-data-source
- Field-tested against a real 1066-record / 69-column database: add the `.xml`
  in New Project Settings, confirm a guessed column mapping once, and from
  then on PA auto-refreshes on every Dekereke edit via the existing
  `ReloadProjectsWhenAppBecomesActivate` machinery.

### Why native support rather than leaving it as an add-on

The add-on works, but only by leaning on things no one should have to lean on:
the undocumented add-on loader; a transient swap of the data source around
`Before/AfterLoadingDataSources`; and reflection into `ProjectSettingsDlg`'s
private members to put a "Dekereke Data Source…" item in the Add dropdown —
necessary because an XML file picked there is typed `XML` and then hits the
*"You must specify an XSLT file"* validation (`ProjectSettingsDlg.cs:362`)
while the XML branch of `DataSourceReader` is a stub (`case
DataSourceType.XML: break;`, `DataSourceReader.cs:274`). Native support
removes every one of those fragilities, and Dekereke users get it without
finding and installing a second program.

### What the change looks like (all line references verified against current `master`)

1. **`DataSourceType.Dekereke`** appended to the enum
   (`PaDataSource.cs:26`). The type is serialized by name, so appending is
   safe for existing `.pap` files.
2. **Detection** in `GetIsXmlFile` (`PaDataSource.cs:351`): a well-formed XML
   file whose root element is `<phon_data>` is a Dekereke database — today it
   falls into the dead `XML` type. Seed `FieldMappings` from the auto-mapper
   at the same point. Illustratively:

   ```csharp
   // in GetIsXmlFile, after the PAXML check:
   if (xmldoc.DocumentElement != null &&
       xmldoc.DocumentElement.Name == "phon_data")
   {
       Type = DataSourceType.Dekereke;
       return true;
   }
   ```

3. **`DekerekeDataSourceReader.cs`**, modeled on `SfmDataSourceReader.cs`
   (227 lines), reading `data_form` elements straight into
   `RecordCacheEntry` objects — no intermediate format. A stream-based
   `XmlReader` handles all three on-disk encodings found in the field
   (UTF-16LE+BOM in older Dekereke releases, UTF-8+BOM, and plain
   no-BOM UTF-8 in the current release).
4. **`DataSourceReader`**: one `case` in the type switch
   (`DataSourceReader.cs:268`) and one branch in `Initialize`
   (`DataSourceReader.cs:54`).
5. **Properties dialog** for the column→field mapping, modeled on
   `SFDataSourcePropertiesDlg`; enable the Properties button and dispatch for
   the new type in `ProjectSettingsDlg`, and add an XML entry to the
   add-data-source file filter (`HandleAddOtherDataSourceClick`,
   `ProjectSettingsDlg.cs:604`).
6. **Tests** plus a small anonymized sample database (I have
   encoding-exact samples ready, covering both the old UTF-16 and current
   UTF-8 formats).

The hard part — parsing the three encoding variants, and auto-mapping
user-defined per-database column names (column inventories differ per
database; the mapper covers English and Indonesian column names) — exists as
a dependency-free netstandard2.0 library, already exercised by a CI test
suite and by live use. **I'm prepared to contribute that code under PA's MIT
license** and to do the integration work myself as a PR, keeping the change
strictly additive.

One thing to acknowledge directly: this would be PA's first non-SIL data
format — today's list (PAXML, FieldWorks, SA, SFM/Toolbox, LIFT) is all SIL
formats and standards. I'd argue Dekereke has earned the exception: it is a
phonology database feeding a phonology tool, the user communities overlap
almost completely, and the format is stable, simple XML.

Would a PR along these lines be welcome? I'm happy to adjust the approach to
whatever the maintainers prefer — and if there's interest, I can have the PR
up quickly, since the underlying code is written and field-tested.

---
