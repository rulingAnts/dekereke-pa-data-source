# Dekereke as a live Phonology Assistant data source

## Context

Seth's Fayu phonology corpus lives in **Dekereke** (Rod Casali's Phonology Database software,
casali.canil.ca), which stores data as XML. Phonology Assistant (SIL) reads Toolbox/SFM, FieldWorks, Speech Analyzer and PaXML —
but not Dekereke.

Seth **already has** a Dekereke → Toolbox SFM converter. It is not the goal, because it produces a
*snapshot*: edit the Dekereke database and PA keeps showing stale data until you re-run the
converter and reload. The goal is Dekereke as a **live data source that auto-refreshes**.

**Target experience, for any Dekereke user, not just Seth:** install PA → run our installer → add
their Dekereke `.xml` as a data source → it works. No XSLT, no config files, no Program Files
spelunking, no hand-written mappings.

**Two deliverables:**

- **Part A — `PaDekereke` add-on.** Works today against stock, unmodified PA 4.1.1, and on every
  already-installed copy in the field. Ships now; needed regardless of what SIL does.
- **Part B — upstream patch** adding `DataSourceType.Dekereke` as a first-class data source. Strictly
  better than the add-on where it matters (see below), but only arrives when SIL merges and ships.

They share the same core library, so Part B is mostly re-wrapping work Part A already needs.

Note on "not forking": opening a PR requires a *throwaway* GitHub fork as the delivery mechanism —
one button, deleted afterward. That is not the same as maintaining a divergent PA, which we are
still not doing. If even a throwaway fork is unwanted, Part B can instead be sent as a patch
attached to a PA JIRA issue.

An earlier draft proposed a different upstream change: finishing PA's dormant `DataSourceType.XML`
+ XSLT path (`ProjectSettingsDlg.cs:227` literally says *"When xslt transforms are supported when
reading data, this should become visible"*). **Dropped** — it's the mechanism Larry remembered, but
it would still require every user to hand-author an XSLT for their own column names. It's a
developer feature, not a Dekereke feature. Part B solves the actual problem.

## The mechanism (all verified in PA's source)

PA has an **undocumented but live** add-on loader — `App.ReadAddOns()` (`App.cs:367`), called from
`PaMainWnd`'s constructor before any project opens. It scans `<install>\AddOns\*.dll` and
instantiates any public class named `PaAddOnManager`. Add-ons reference PA; PA knows nothing of them.

The auto-refresh chain already exists in stock PA and is **on by default**
(`ReloadProjectsWhenAppBecomesActivate` = True):

```
PA regains focus
  → PaProject.HandleApplicationWindowActivated            (PaProject.cs:484)
  → CheckForModifiedDataSources()                         (PaProject.cs:496)
  → ds.UpdateLastModifiedTime()   — File.GetLastWriteTimeUtc(SourceFile)
  → PostMessage("ReloadProject")
  → LoadDataSources()                                     (PaProject.cs:530)
  → SendMessage("BeforeLoadingDataSources")               ← our add-on runs here
  → new DataSourceReader(this); reader.Read()
  → SendMessage("AfterLoadingDataSources")                ← our add-on restores here
```

**This is why the data source must remain the Dekereke `.xml`.** PA watches
`ds.SourceFile`'s timestamp. If we permanently repointed the data source at a generated SFM file,
PA would watch *that* — and we'd have rebuilt Seth's existing snapshot converter with extra steps.
So: PA holds the Dekereke file; the add-on converts to SFM **transiently, inside each load**, and
restores afterward.

`PaProject.DataSources` is `public List<PaDataSource> { get; set; }` (`PaProject.cs:1013`), so the
list can be rewritten in the gap. The shelved `src/AddOns/PaDataSourceUtilsAddOn` does exactly this
remove/restore dance around the same two messages — this is the hook's intended use.

## The hard problem: mappings can't be hard-coded

Dekereke column names are **user-defined per database**. Seth's Fayu DB has ~70 columns; Barnabas's
has ~16; they share only `Reference`, `Category`, `SoundFile`, `IndonesianGloss`, `Phonetic`,
`Tulisan`. Any fixed transform silently drops most of a stranger's data.

So we ship a **mapper**, not a transform: read the real column names out of the user's file at
runtime, auto-map by heuristic, and let them adjust in a dialog. XSLT never enters the user's world.

## Why Toolbox SFM as the intermediate

PA's SFM importer is its most mature, and the format is trivial to emit correctly. Crucially,
the add-on can bypass PA's mapping UI entirely: `FieldMapping(string nameInSource, PaField field,
bool isParsed)` is public (`FieldMapping.cs:52`) and `PaDataSource.FieldMappings` is public and
settable, so we assign mappings programmatically — including for PA fields that have **no**
`possibleDataSourceFieldNames` in `DefaultFields.xml` (Phonemic, Orthographic, Note), which PA
could never auto-map on its own.

Constraints confirmed from `SfmDataSourceReader.cs:72-96`:
- One marker per line, `\mkr value`; **a line not starting with `\` is silently skipped**, so any
  newline inside a Dekereke value must be flattened to a space.
- Records are keyed into a `Dictionary`, so **a marker must not repeat within a record** — last wins.
- Empty values are skipped by the reader anyway.
- `File.ReadAllLines` is used for both marker discovery and reading → write **UTF-8 with BOM**
  (matches the shipped `Sekpele 2.db` sample). Never UTF-16.
- Record marker: `DefaultSfmRecordMarker` = `\ref` (a single value, so `SingleOrDefault` in
  `PaDataSource.cs:101` can't throw). Emit `\ref` and a `\_sh v3.0  400  PhoneticData` header so
  PA types it `Toolbox`.
- `DefaultParsedSfmFields` = `Phonetic;Phonemic;Gloss;Gloss-Secondary;Gloss-Other;PartOfSpeech;Tone;Orthographic`
  → set `IsParsed` for those.

## Architecture

The core library lives in Seth's own repo (working dir `/Users/Seth/dekereke-pa-data-source`,
empty) and has **no PA dependency**, so it serves both parts: Part A wraps it with an SFM writer
plus mediator glue; Part B wraps it as a native reader plus a mapping dialog. The add-on references
the installed `Pa.exe` and `SilTools.dll` as assemblies — no PA source tree needed for Part A.

```
src/
  DekerekeToPa/          core library, no PA dependency — unit-testable in isolation
    DekerekeFile.cs      sniff (root == phon_data), stream records, enumerate columns
    ColumnMap.cs         mapping model + load/save
    AutoMapper.cs        name heuristics
    SfmWriter.cs         emit Toolbox SFM
  DekerekeToPa.UI/       WinForms mapping dialog
  PaDekereke/            the add-on: PaAddOnManager + PA glue
installer/               Inno Setup script
```

### `DekerekeFile` — reading

Sniff and read with a **stream-based** `XmlReader` (`File.OpenRead` → `XmlReader.Create(stream)`),
never `File.ReadAllText`. This is the one real trap: older Dekereke writes **UTF-16LE + BOM** with
`encoding="utf-16"` in the declaration; the current release writes **plain UTF-8 with no BOM**,
where the declaration is the only encoding signal. A string already decoded from
UTF-16 still carries that declaration and `XmlReader` throws *"There is no Unicode byte order
mark."* Reading from the stream sniffs the BOM and handles both with no version check.

Columns = union of child element names across `data_form` elements (not just the first record —
Dekereke omits nothing per-record in practice, but don't rely on it). Ignore the nested
`<qvp_acoustic_data_>` block. Optionally read column order from the sibling `*-DkUserSettings.xml`.

### `AutoMapper` — heuristics

Exact, then case-insensitive, then a synonym table covering both English and Indonesian column
names (Dekereke is used across Indonesian-language projects — Seth's own DBs contain `Tulisan`,
`Nada`, `Catatan`):

| PA field | matches |
|---|---|
| `Phonetic` | Phonetic, Fonetik |
| `Tone` | Pitch, Tone, Nada, Surface_Melody |
| `Phonemic` | Phonemic, Fonemik |
| `Gloss` | Gloss, Arti |
| `Gloss-Secondary` | IndonesianGloss, Gloss2, ArtiIndonesia |
| `PartOfSpeech` | Category, POS, Kategori |
| `Reference` | Reference, Ref, No |
| `Orthographic` | Orthography, Tulisan |
| `AudioFile` | SoundFile, Audio |
| `Note` | Notes, Catatan, OrthWkshpNotes |

Unmatched columns default to unmapped. Phonetic is required; everything else optional.
For Seth's Fayu DB this yields the mapping he already confirmed, with the elicitation-frame columns
(`goodX`, `whiteX`, `Xbad`, `Xpig`, `macheteX`, …) left unmapped.

### `SfmWriter`

Emit markers from the map: `\ref \ph \tn \pm \ge \gn \ps \or \sf \nt`. Flatten newlines to spaces,
skip empty values, skip records with no phonetic, guarantee no duplicate marker per record.
UTF-8 with BOM. Write to `%LOCALAPPDATA%\PaDekereke\<hash>\<name>.db` — a cache directory, not the
user's project folder and not Program Files.

### `PaDekereke` — the add-on

```csharp
public class PaAddOnManager : IxCoreColleague          // SilTools.IxCoreColleague
{
    public PaAddOnManager() { App.AddMediatorColleague(this); }        // App.cs:690
    public IxCoreColleague[] GetMessageTargets() { return new[] { this }; }

    protected bool OnBeforeLoadingDataSources(object args) { …; return false; }
    protected bool OnAfterLoadingDataSources(object args)  { …; return false; }
}
```

`IxCoreColleague` requires only `GetMessageTargets()`; handlers are `On<Message>(object)` returning
`bool` (`false` = keep propagating). Verified against the legacy add-on's own handlers.

**On Before**, for each data source whose file sniffs as Dekereke:
1. Capture the original `PaDataSource` object, its index, and
   `File.GetLastWriteTimeUtc(dekerekeFile)`.
2. Load the saved mapping from `<PA project folder>\<project>.DekerekeMappings.xml`. If absent —
   or if `Phonetic` ends up unmapped after auto-mapping newly-appeared columns — close the splash
   (`App.CloseSplashScreen()`) and show the mapping dialog once, then save. Otherwise **never
   prompt**: after a Dekereke edit this path runs on every app focus, and a dialog there would be
   unbearable. New columns are auto-mapped silently.
3. Convert to SFM.
4. Replace the list entry with a **new** `PaDataSource(project.Fields, sfmPath)` (leaving the
   original object untouched so its cached `m_markersInFile` isn't disturbed), then overwrite its
   `FieldMappings` from our map and set `TotalLinesInFile` to the record count.

**On After:**
1. Put the original `PaDataSource` back at its index.
2. Set its `LastModification` to the **Dekereke** file's captured mtime. Critical: during the
   window `DataSourceReader` stamps the *SFM* file's mtime onto the data source
   (`DataSourceReader.cs:287`); leave that in place and PA either never reloads again or reloads
   forever.
3. `project.Save()` — see the risk below.

**Verified risk — a mid-window `.pap` save.** `ProjectInventoryBuilder.cs:221` calls
`_project.Save()`, reached from `reader.Read()` → `BuildWordCache` → `ProjectInventoryBuilder.Process`,
guarded by `if (!File.Exists(<project>.PhoneticInventory.xml))` — i.e. **on first load of a new
project**, right in the middle of our swap. That would bake the temp SFM path into the `.pap`.
Mitigation: the unconditional `project.Save()` in step 3 rewrites it with the true Dekereke path.
Belt-and-braces: capture the `.pap` bytes on Before and compare on After.

**Re-mapping.** `App.TMAdapter` is a public static `ITMAdapter` (`App.cs:727`), so the add-on can
add its own "Dekereke Mappings…" menu item to reopen the dialog — the add-on loader's own docs say
menu additions are the add-on's job. This is the discoverability answer; no file deletion, no
config editing.

**Coexistence:** no-op cleanly if a future PA handles `DataSourceType.XML` itself (probe the loaded
PA assembly for `XmlDataSourceReader`).

---

## Part B — upstream patch: `DataSourceType.Dekereke`

Everything Part A achieves by prying at an undocumented hook, Part B gets natively. **Auto-refresh
comes for free**, because the data source genuinely *is* the Dekereke file — no swap, no restore,
no `ProjectInventoryBuilder.cs:221` `.pap` hazard, no dependence on `App.ReadAddOns()` surviving.
It is the better design; it just isn't available until SIL ships it.

PA's existing data-source types are the template — this change adds one more the same way FW7 was
added, touching no existing behaviour:

1. **`DataSourceType.Dekereke`** appended to the enum (`PaDataSource.cs:26`). Safe for existing
   projects: `Type` is serialized by *name* via `XmlSerializer`, and `GetPaXmlType` parses by name
   (`PaDataSource.cs:410`) — so ordinal position doesn't matter. Verify with an existing `.pap`.
2. **Detection** in `PaDataSource.GetIsXmlFile()` (`:351`): root element `phon_data` → `Dekereke`,
   and seed `FieldMappings` from the shared auto-mapper — so a Dekereke file dropped into "Add data
   source" is recognised and largely mapped before the user opens any dialog.
3. **`DekerekeDataSourceReader.cs`**, modeled on `SfmDataSourceReader.cs` (227 lines). Reads the
   Dekereke XML **straight into `RecordCacheEntry` objects** — no SFM intermediate at all, which is
   cleaner than Part A and drops the whole class of marker-collision and newline-flattening
   constraints. Same stream-based `XmlReader` for the UTF-16LE/UTF-8/no-BOM handling.
4. **`DataSourceReader`**: one `case` in the switch (`:268`), one branch in `Initialize()` (`:107`).
5. **`DekerekeDataSourcePropertiesDlg`** — the column→field mapping grid, modeled on
   `SFDataSourcePropertiesDlg.cs` (661 lines, but much of that is SFM-only: interlinear parse types,
   record-marker choice, editor selection). Expect ~250–350 lines.
6. **`ProjectSettingsDlg`**: two small edits — enable the Properties button for the new type
   (`:285`) and add one branch to the dispatch chain (`:759`). Add `*.xml` to the Add-Data-Source
   file-type filter (`:604`) so Dekereke files are easy to pick.
7. **`Pa.csproj`** entries; **tests** plus a small Dekereke sample in `src/PaTests/TestFiles/Input/`.

**Acceptance risk, stated plainly:** Dekereke would be PA's first **non-SIL** data format — today's
list (PAXML, FW6, FW7, SA, SFM, Toolbox, LIFT) is entirely SIL formats and standards. That, not
code quality, is what could stall it. Mitigations: file a PA JIRA issue (`jira.sil.org/browse/PA`)
and get maintainer buy-in *before* building the dialog; keep the change purely additive; lead with
the fit argument (Dekereke is a phonology database, PA is a phonology tool, the users are the same
people); and loop in Larry Hayashi, who already pointed at this area.

**Prospects are better than I first said.** I previously cited the dependabot PRs open since 2022 as
evidence of a stalled repo — that was misleading. Those are unattended bot PRs. Human contributions
move: PR #32 merged in 6 days, PR #44 in 5, both by maintainer `gtryus`. There is no CONTRIBUTING.md
and no CLA in the repo; PA is MIT.

**Sequencing.** Build the shared core first and prove it against real Fayu and Barnabas data. Then
Part A, since it's the one that helps anyone this year. File the JIRA issue in parallel, and start
Part B only once a maintainer signals interest — the properties dialog is the bulk of Part B's cost
and shouldn't be written speculatively.

---

### Installer

Inno Setup, single elevated `.exe` (PA installs per-machine, `ALLUSERS=1`). Locate PA via its MSI
UpgradeCode `{5E57E4D4-580A-4cc1-9E0C-7EF8D3F81BBD}` (fixed across PA versions —
`Installer/Product.wxs:9`): `MsiEnumRelatedProducts` → `MsiGetProductInfo(INSTALLPROPERTY_INSTALLLOCATION)`,
falling back to `%ProgramFiles(x86)%\SIL\Phonology Assistant`, then a browse dialog. Install
`PaDekereke.dll` + `DekerekeToPa*.dll` into `<PA>\AddOns\`; uninstaller removes them. Refuse to
install with a clear message if PA isn't found.

## Build & verification

No .NET toolchain on this Mac (`dotnet`, `mono`, `csc` all absent), so all compilation happens in
the **Windows 11 Parallels VM** (the only VM present). Drive it with `prlctl exec` — no SSH key
involved, so the FlexTools-bridge key `~/.ssh/id_ed25519` stays untouched per standing policy;
Seth can equally build in Visual Studio. Targets .NET Framework 4.8, x86 (must match `Pa.exe`).

1. **Core library tests** (no PA needed): records in every encoding variant — UTF-16LE+BOM (older
   releases), UTF-8+BOM, and plain UTF-8 with no BOM (current release); column enumeration; auto-map results; SFM output (no duplicate markers, no bare newlines,
   UTF-8 BOM present, `\_sh` header, `\ref` boundaries).
2. **Cold start:** fresh PA project, add `Fayu_stable.xml`, expect the mapping dialog once, then a
   populated Data Corpus, an auto-generated CV chart, `Pitch` in the Tone column, and audio playing
   via `SoundFile`.
3. **The whole point — live refresh:** with PA open, edit a form in Dekereke and save; alt-tab to
   PA; it should reload by itself and show the change with **no dialog and no manual step**.
4. **`.pap` integrity:** after step 2 (the new-project case that trips
   `ProjectInventoryBuilder.cs:221`) and after step 3, confirm the `.pap` still names
   `Fayu_stable.xml` and not a temp path.
5. **Second database:** repeat with `Barnabas-DekerekeBackup2.xml` — a genuinely different column
   set — to prove nothing is hard-coded to Fayu.
6. **Installer:** install on a clean Windows VM snapshot with PA present; confirm detection,
   AddOns placement, and clean uninstall.
7. **No-PA regression:** with the add-on installed, open a Toolbox project
   (`Sekpele 2.db`) and confirm PA behaves exactly as before.

## Open question — SETTLED 2026-08-10

How large a Dekereke file stays acceptable? The conversion runs on **every** reload, i.e. every time
PA regains focus after a Dekereke edit. Seth's Fayu DB is 3.3 MB / ~2000 records — measure it; if
conversion is slow, cache by source mtime + mapping hash and skip regeneration when neither changed.

**Measured on the real Fayu database** (1066 records, 69 columns, from the add-on's own log):
read + auto-map + SFM write completes within the same second it starts, and the whole
convert → PA read → restore cycle takes 1–2 s. That is well inside what a focus-triggered reload can
absorb, so **the mtime/mapping-hash cache is not needed** and was not built. Revisit only if a
database an order of magnitude larger shows up.
