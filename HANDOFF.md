# HANDOFF — technical briefing

Everything a developer (human or agent) needs to continue this project without
re-deriving it. All PA references are to `sillsdev/phonology-assistant` @
`master` (PA 4.1.1, .NET Framework 4.8, WinForms, x86, no CI in that repo).
Read `docs/PLAN.md` for the full design rationale; this file is the working
engineer's version.

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

1. **Encoding.** Current Dekereke writes UTF-16LE + BOM with
   `encoding="utf-16"` declaration; newer versions write UTF-8. Always hand the
   RAW STREAM to `XmlReader`/`XDocument`. Pre-decoding to a string leaves a
   `utf-16` declaration on in-memory UTF-16-decoded text and `XmlReader` throws
   *"There is no Unicode byte order mark."*
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

## State of the code (as of handoff)

| Piece | State |
|---|---|
| `src/DekerekeToPa` core (reader, auto-mapper, SFM writer, cache, mapping store) | Written; compiles nowhere yet (authored on a Mac with no .NET SDK) — expect only trivial fixes |
| `src/DekerekeToPa.Tests` | Written (NUnit, net8.0). **First task: `dotnet test` and make green** |
| `src/PaDekereke` add-on + mapping dialog | Written against verified PA APIs; **never compiled** — needs `Pa.exe`/`SilTools.dll` |
| `installer/PaDekereke.iss` | Drafted; MSI-based PA detection stubbed (TODO comment inside), path-probe fallback works |
| `sample-data/` | Generated, encoding-verified (UTF-16LE+BOM and UTF-8+BOM, CRLF) |
| Part B (native PA patch) | Designed only — see `docs/PLAN.md`; do not start without maintainer buy-in |

## Getting PA assemblies without a PA install

The add-on references the installed `Pa.exe` and `SilTools.dll`. On a machine
without PA (CI, cloud agents):

1. Download the PA installer from https://software.sil.org/phonologyassistant/download/
2. Linux: `msiextract` (from `msitools`) unpacks MSIs; the installer may be an
   exe wrapper around an MSI — `7z x` handles most wrappers.
3. Point the build at the extracted folder:
   `dotnet build src/PaDekereke -c Release -p:PaInstallDir=/path/to/extracted/`

`Microsoft.NETFramework.ReferenceAssemblies` makes net48 compile on any OS
(compile only; running needs Windows).

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
