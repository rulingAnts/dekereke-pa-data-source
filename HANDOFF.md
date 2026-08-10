# HANDOFF — technical briefing

Everything a developer (human or agent) needs to continue this project without
re-deriving it. All PA references are to `sillsdev/phonology-assistant` @
`master` (PA 4.1.1, .NET Framework 4.8, WinForms, x86, no CI in that repo).
Read `docs/PLAN.md` for the full design rationale; this file is the working
engineer's version.

**Working offline?** `docs/pa-internals/` vendors what you would otherwise need
the PA repository for: `api-surface.md` (exact signatures of every PA type the
add-on touches, enough to build a stub assembly) and `hook-points.md` (the PA
source excerpts behind the claims below). PA's assemblies are not and cannot be
committed here.

## The mechanism, verified line by line

**Add-on loader.** `App.ReadAddOns()` — `src/Pa/App.cs:367`, called from
`PaMainWnd`'s constructor before any project loads. Scans
`<install>\AddOns\*.dll`, instantiates any public class named `PaAddOnManager`
(reflection, `ReflectionHelper.CreateClassInstance(assembly, "PaAddOnManager")`),
each load wrapped in try/catch. Undocumented but shipped and live in 4.1.1.

**Load pipeline.** `PaProject.LoadDataSources` — `src/Pa/Model/PaProject.cs:530`:

```csharp
App.MsgMediator.SendMessage("BeforeLoadingDataSources", this);  // ← add-on converts+swaps here
var reader = new DataSourceReader(this);
RecordCache = reader.Read();
...
App.MsgMediator.SendMessage("AfterLoadingDataSources", this);   // ← add-on restores here
```

Handlers are dispatched by name: a colleague registered via
`App.AddMediatorColleague(IxCoreColleague)` (`App.cs:690`) gets
`On<MessageName>(object args)` invoked (`SilTools.Mediator`/`MessageDispatcher`
prepend `"On"`). `IxCoreColleague` (namespace `SilTools`) has one member:
`GetMessageTargets()`. The abandoned `src/AddOns/PaDataSourceUtilsAddOn` in the
PA repo does the same remove/restore dance around these exact messages.

**Auto-refresh.** `PaProject.HandleApplicationWindowActivated`
(`PaProject.cs:484`) → `CheckForModifiedDataSources()` (`:496`) →
`ds.UpdateLastModifiedTime()` (compares `File.GetLastWriteTimeUtc(SourceFile)`
to `ds.LastModification`) → `PostMessage("ReloadProject")` → full reload
including our hooks. Gated by user setting
`ReloadProjectsWhenAppBecomesActivate`, **default True**. This is why the
project's data source must remain the Dekereke file itself — swap permanently
and PA watches the wrong file, and you have rebuilt a snapshot converter.

**Public surface the add-on relies on** (all verified):
- `PaProject.DataSources` — `public List<PaDataSource> { get; set; }` (`PaProject.cs:1013`)
- `PaProject.Fields` — `public IEnumerable<PaField> { get; private set; }` (`:1027`)
- `PaProject.Folder` (`:854`), `.Name` (`:952`), `.Save()` (`:660`)
- `PaDataSource(IEnumerable<PaField> fields, string filename)` ctor — sniffs
  file content: XML parse attempt first, then SFM/Toolbox detection
  (`PaDataSource.cs:84`)
- `PaDataSource.FieldMappings` — public settable; `SfmRecordMarker`,
  `TotalLinesInFile`, `SkipLoading`, `SkipLoadingBecauseOfProblem` ([XmlIgnore]),
  `LastModification` ([XmlIgnore]) — all public settable
- `FieldMapping(string nameInSource, PaField field, bool isParsed)` —
  `src/Pa/Model/Fields/FieldMapping.cs:52`
- `App.CloseSplashScreen()` (`App.cs:668`); `App.AddMediatorColleague` (`:690`)

**SFM reader constraints** (from `src/Pa/DataSourceClasses/SfmDataSourceReader.cs:72-96`):
- reads with `File.ReadAllLines`; non-`\`-initial lines silently dropped →
  never emit raw newlines inside values
- per-record lines keyed into a `Dictionary<marker, value>` → a marker must not
  repeat within a record
- a record-marker line with an empty value fails the split-in-two parse and is
  dropped → two records silently merge; synthesize a Reference when empty
- record marker `\ref` matches PA's `DefaultSfmRecordMarker` app setting;
  `\_sh v3.0  400  PhoneticData` header ⇒ Type = Toolbox
- parsed-by-default fields (`DefaultParsedSfmFields` setting):
  `Phonetic;Phonemic;Gloss;Gloss-Secondary;Gloss-Other;PartOfSpeech;Tone;Orthographic`

**PA fields with no auto-map markers**: `Phonemic`, `Orthographic`, `Note` have
no `possibleDataSourceFieldNames` in `DistFiles/Configuration/DefaultFields.xml`
— PA alone can never map them from SFM. We map them programmatically (markers
`\pm`, `\or`, `\nt` are ours).

## The traps (each cost real investigation — do not rediscover them)

1. **Encoding.** Three variants are in the field, and nothing else about the
   format differs between them:
   - **UTF-16LE + BOM**, `encoding="utf-16"` declaration — older Dekereke
     releases, still widely in use (Seth's own working database is one)
   - UTF-8 + BOM — intermediate
   - **Plain UTF-8, no BOM** — current release (Rod Casali changed the output
     encoding in the 2026 release at Seth's request)

   Always hand the RAW STREAM to `XmlReader`/`XDocument` and let it resolve the
   encoding from BOM + declaration. Pre-decoding to a string leaves a `utf-16`
   declaration on already-decoded text and `XmlReader` throws *"There is no
   Unicode byte order mark."* Equally, do not assume a BOM exists — the current
   format has none, and the declaration is the only signal.
   `DekerekeFileTests.Read_AllEncodingVariants_YieldIdenticalContent` and
   `SfmWriterTests.Write_AllSourceEncodings_ProduceIdenticalOutput` pin this.
2. **Mid-load `.pap` save.** `ProjectInventoryBuilder.cs:221` calls
   `_project.Save()` during the FIRST load of a project — i.e. inside our swap
   window — baking the temp SFM path into the `.pap`. Mitigated by an
   unconditional `project.Save()` after restore in `OnAfterLoadingDataSources`.
   Verify on a fresh project (verification list below).
3. **`LastModification` stamping.** During the read, PA stamps the (temp) data
   source's mtime (`DataSourceReader.cs:287`). On restore, set the ORIGINAL data
   source's `LastModification` to the Dekereke file's mtime captured at
   conversion time — else PA reloads forever or never.
4. **Records keep a reference to the temp `PaDataSource`** (per-record
   `DataSource` property drives the UI's DataSource/DataSourcePath columns via
   `RecordCacheEntry.GetValue`, `RecordCacheEntry.cs:149`). Cosmetic fix applied:
   after the read, point the temp object's `SourceFile` back at the Dekereke
   path; also the cache file keeps the original file name (see
   `ConversionCache.cs`).
5. **Dekereke column names are user-defined per database.** The two real
   databases this was built against share only ~6 of ~70 and ~16 columns. Never
   hard-code a mapping; `AutoMapper` + the dialog is the design. Column names
   can contain `-` and `.` (`IMP-re`, `TS.practice` in real data — exercised by
   `sample-data/`).
6. **Prompt discipline.** The convert path runs on EVERY focus-triggered
   reload. It must never prompt except: first contact with a database, phonetic
   unmappable, or Shift held during load.
7. **ProjectSettingsDlg blocks XML sources before the add-on can run**
   (found live on a real PA install, 2026-08-10). The add-on hooks project
   *loading*, but a Dekereke `.xml` cannot *enter* a project through the UI:
   the add-data-source picker has no `*.xml` filter (browse via "All Files"
   works), the file is typed `XML`, and clicking OK raises **"You must
   specify an XSLT file for '<file>'"** — from PA's dormant XSLT path
   (`hook-points.md` §6), whose "Specify XSLT" column is hidden and whose
   reader ignores XML sources anyway (`DataSourceReader.cs:274`:
   `case DataSourceType.XML: break;`). So no XSLT — supplied, hand-edited
   into the `.pap`, or referenced by an `xml-stylesheet` declaration inside
   the Dekereke file — can ever make stock PA read it; PA validates and
   stores the XSLT but never applies it. Consequences:
   - The cold-start step of the checklist below CANNOT pass through the UI
     as written. Workaround for testing: create the project with some other
     source (e.g. a `DekerekeConvert` snapshot `.db`), close PA, hand-add a
     second `<DataSource>` entry with `<Type>XML</Type>` pointing at the
     Dekereke file in the `.pap`, reopen — project *open* does not go
     through the dialog validation, and the add-on swaps the source before
     the reader would skip it. (Not yet verified on Windows.)
   - The real fix — an add-on-owned "Add Dekereke Data Source" menu item
     via `App.TMAdapter` — is IMPLEMENTED (2026-08-10, `ITMAdapter`
     transcribed from a source clone; see `api-surface.md`, "Menus").
     The `.pap` hand-edit above remains only as a fallback/diagnostic.
   - Debugging aid: the add-on logs to `%LOCALAPPDATA%\PaDekereke\addon.log`
     ("constructed" = loader ran it; "registered" = Pa.exe/SilTools bound;
     conversion lines = pipeline live). No file at all = the loader never
     instantiated it (missing/wrong AddOns folder, or a strong-name binding
     failure at type load - check with
     `[Reflection.AssemblyName]::GetAssemblyName('...\Pa.exe').FullName`).
   - **Confirmed live 2026-08-10** on an installed PA 4.1.1 (Win11-ARM VM):
     the loader instantiated the add-on from a hand-created `AddOns` folder,
     and the STUB-compiled build bound to the real `Pa.exe`/`SilTools.dll`
     and executed `App.AddMediatorColleague` - so the shipped PA is not
     strong-named in a way that blocks stub-built references, and the
     `api-surface.md` signatures exercised so far are correct at run time.
   - **The `.pap` workaround itself is also confirmed live** (same day, real
     Fayu database, 944 records): project open → mapping dialog → convert →
     PA read the swap → restore, end to end. It exposed trap 8 below.
8. **Duplicate add-on instances and overlapping loads are real; know the
   two causes** (observed live 2026-08-10; cause pinned from source the
   same day). A first live test showed two `constructed` lines seconds
   apart and two identical mapping dialogs whose OKs resolved in
   sequence. Source facts: `App.ReadAddOns()` is called only from the
   `PaMainWnd` constructor (`PaMainWnd.cs:80`), and `PaMainWnd` is
   constructed exactly once per process (`Program.cs:42`) - so the double
   instantiation was almost certainly **two PA processes** sharing the
   log (each auto-loading the last project, each prompting). The add-on
   now logs its process id to make this unambiguous. Separately,
   **same-process re-entrancy is still possible**: a modal dialog inside
   `OnBeforeLoadingDataSources` pumps messages, and a focus-triggered
   `ReloadProject` can start a second load pipeline mid-dialog. Defenses
   implemented in `PaAddOnManager` and worth keeping regardless of cause:
   register only the first instance per process (static singleton), keep
   per-load swap state on a LIFO stack (restore is swap-driven, so even
   mispaired pops restore correctly), and never stack a second mapping
   dialog for a database whose dialog is already open (the nested pass
   silently uses the auto-map). Cross-process duplication cannot be
   guarded from inside one process; it is benign (the mapping store's
   last writer wins).

## State of the code (as of handoff)

| Piece | State |
|---|---|
| `src/DekerekeToPa` core (reader, auto-mapper, SFM writer, cache, mapping store) | Written; compiles nowhere yet (authored on a Mac with no .NET SDK) — expect only trivial fixes |
| `src/DekerekeToPa.Tests` | Written (NUnit, net8.0). **First task: `dotnet test` and make green** |
| `src/PaDekereke` add-on + mapping dialog | Written against verified PA APIs; **never compiled** — needs `Pa.exe`/`SilTools.dll` |
| `installer/PaDekereke.iss` | Drafted; MSI-based PA detection stubbed (TODO comment inside), path-probe fallback works |
| `sample-data/` | Generated, encoding-verified (UTF-16LE+BOM = older releases, plain UTF-8 no BOM = current release, CRLF) |
| Part B (native PA patch) | Designed only — see `docs/PLAN.md`; do not start without maintainer buy-in |

## Building the add-on without a PA install

The add-on references the installed `Pa.exe` and `SilTools.dll`. Two routes:

**With network access** — download the PA installer from
https://software.sil.org/phonologyassistant/download/ ; on Linux `msiextract`
(from `msitools`) unpacks MSIs, and `7z x` handles most exe wrappers. Then
`dotnet build src/PaDekereke -c Release -p:PaInstallDir=/path/to/extracted/`.

**Offline** — build a stub assembly from the signatures in
`docs/pa-internals/api-surface.md` (recipe at the end of that file) and build
with `-p:UseStubs=true`. This type-checks the add-on; it proves nothing about
run-time behaviour, and results must be reported as such. The stub must never
be shipped or installed.

Either way `Microsoft.NETFramework.ReferenceAssemblies` makes net48 compile on
any OS — compile only; running needs Windows.

## Owner requirements (confirmed 2026-08-10)

1. Field mapping must offer **common-sense defaults** (the auto-mapper's
   guesses) but be **user-customizable both at import time and later, when
   configuring the data source**. The dialog-on-first-contact covers import
   time; "later" is currently only Shift-held-during-load, which is not
   discoverable — the planned "Dekereke Mappings…" menu item (via
   `App.TMAdapter`, blocked on transcribing `ITMAdapter` from PA source) is
   required, not optional polish.
2. **A discoverable "add a Dekereke file" path with a `*.xml` file type,
   validating that the picked XML really is Dekereke** (root `phon_data`,
   i.e. `DekerekeFile.Sniff`) before accepting it. PA's own add-data-source
   picker cannot be extended by an add-on (its filter is hard-coded in
   `ProjectSettingsDlg`, and XML picks dead-end at the XSLT error — trap 7),
   so this means an add-on-owned "Add Dekereke Data Source…" menu item with
   its own picker + Sniff validation, which appends the `PaDataSource`,
   saves, and triggers a reload.
   **IMPLEMENTED 2026-08-10** (`ITMAdapter` transcribed from a source clone —
   see `api-surface.md`, "Menus"): Tools menu → "Add Dekereke Data Source…",
   placed before Options; greyed until a project is open; picker filtered to
   `*.xml`; Sniff-validated with a clear message on rejection; duplicate-path
   check; then `DataSources.Add(new PaDataSource(fields, path))` +
   `project.Save()` + `ReloadDataSources()` — the standard pipeline, mapping
   dialog on first contact, **no `.pap` hand-editing**. Compile-checked only;
   needs the live PA pass. Part B is what would put "Dekereke (*.xml)" in
   PA's native picker.

## Remaining work, in priority order

1. `dotnet restore && dotnet test src/DekerekeToPa.Tests` — fix to green.
2. Compile the add-on (see above) and fix what the compiler finds. Do not
   change the PA-facing logic without re-reading the relevant PA source.
3. GitHub Actions: ubuntu job running the tests; optionally a windows job that
   downloads+extracts PA and builds the add-on artifact.
4. Installer: implement the MSI UpgradeCode lookup
   (`{5E57E4D4-580A-4cc1-9E0C-7EF8D3F81BBD}`, stable across PA versions —
   `Installer/Product.wxs:9` in the PA repo), build with `iscc`.
5. Windows verification against a live PA (needs a human or a Windows VM with
   PA installed) — checklist below.
6. Part B (upstream `DataSourceType.Dekereke` in PA itself): file a PA JIRA
   issue (https://jira.sil.org/browse/PA) first; design in `docs/PLAN.md`.

## Windows verification checklist

1. **Cold start**: fresh PA project → add `sample-data/SampleLang_full.xml` as
   data source → mapping dialog appears once → Data Corpus populated, CV chart
   generated, Pitch under Tone, audio path prefixed from DkUserSettings.
   **KNOWN BLOCKED via the UI** — see trap 7; use the `.pap` hand-edit
   workaround there until the add-on grows its own add-source entry point.
2. **Live refresh (the whole point)**: with PA open, touch/edit the Dekereke
   file, refocus PA → auto-reload, updated data, **no dialog**.
3. **`.pap` integrity**: after (1) — the case that trips the mid-load save —
   and after (2), the `.pap` must reference `SampleLang_full.xml`, never a
   `%LOCALAPPDATA%\PaDekereke\...` path.
4. **Second database**: `SampleLang_minimal.xml` (different columns, UTF-8) in
   the same project.
5. **Cancel path**: cancel the first-time mapping dialog → source skipped,
   PA otherwise fine, dialog re-offered next load.
6. **Regression**: a plain Toolbox/SFM project with the add-on installed
   behaves exactly as stock.
7. **Shift-reload**: hold Shift while opening the project → dialog reopens.

## Style

Tabs, `m_`/`_` fields, one class per concern — match the PA codebase feel; the
core library must stay dependency-free and PA-free. Keep everything
C# 7.3-compatible (netstandard2.0/net48 default LangVersion).
