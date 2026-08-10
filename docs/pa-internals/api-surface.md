# PA API surface used by the add-on

Exact declarations, transcribed from PA 4.1.1 source. This is everything
`src/PaDekereke` touches. It is sufficient to build a **stub assembly** that
lets `PaDekereke` type-check without the real `Pa.exe` — see the bottom of this
file.

Assembly layout: `Pa.exe` contains namespaces `SIL.Pa`, `SIL.Pa.DataSource`,
`SIL.Pa.Model`. `SilTools.dll` contains `SilTools`.

---

## `SilTools` (SilTools.dll)

```csharp
namespace SilTools
{
    public interface IxCoreColleague
    {
        IxCoreColleague[] GetMessageTargets();
    }

    public sealed class Mediator : IDisposable
    {
        public void AddColleague(IxCoreColleague colleague);
        public void RemoveColleague(IxCoreColleague colleague);
        public object SendMessage(string message, object parameter);
        public void PostMessage(string message, object parameter);
    }
}
```

**Message dispatch convention:** `SendMessage("Foo", arg)` invokes
`OnFoo(object arg)` on each registered colleague. The handler may be
`protected`; it returns `bool` — `true` stops propagation, `false` continues.
(`MessageDispatcher` prepends `"On"` to the message name.)

## `SIL.Pa` (Pa.exe)

```csharp
namespace SIL.Pa
{
    public static class App
    {
        public static Mediator MsgMediator { get; internal set; }
        public static void AddMediatorColleague(IxCoreColleague colleague);
        public static void RemoveMediatorColleague(IxCoreColleague colleague);
        public static void ReadAddOns();
        public static void CloseSplashScreen();
        public static string AssemblyPath { get; }
        public static string ProjectFolder { get; set; }
        public static ITMAdapter TMAdapter { get; set; }
        public static List<Assembly> AddOnAssemblys { get; private set; }
        public static List<object> AddOnManagers { get; private set; }
    }
}
```

## `SIL.Pa.DataSource` (Pa.exe)

```csharp
namespace SIL.Pa.DataSource
{
    public enum DataSourceType
    {
        PAXML, FW, FW7, SA, SFM, Toolbox, XML, LIFT, Unknown
    }

    public enum DataSourceParseType
    {
        PhoneticOnly, None, OneToOne, Interlinear
    }

    [XmlType("DataSource")]
    public class PaDataSource
    {
        public const string kRecordMarker = "RecMrkr";
        public const string kShoeboxMarker = "\\_sh ";

        public PaDataSource();
        public PaDataSource(IEnumerable<PaField> projectFields, FwDataSourceInfo fwDbItem);
        public PaDataSource(IEnumerable<PaField> fields, string filename);

        public PaDataSource Copy();
        public IEnumerable<string> GetSfMarkers(bool showMsgOnError);
        public bool VerifyMappings();
        public bool UpdateLastModifiedTime();
        public static DataSourceType GetPaXmlType(string filename, out string fwServer, out string fwDBname);

        [XmlElement("DataSourceFile")]
        public string SourceFile { get; set; }
        public DataSourceType Type { get; set; }
        public DataSourceParseType ParseType { get; set; }
        public string SfmRecordMarker { get; set; }
        public string XSLTFile { get; set; }
        public string FirstInterlinearField { get; set; }
        public int TotalLinesInFile { get; set; }
        public string ToolboxSortField { get; set; }
        public string Editor { get; set; }
        public bool SkipLoading { get; set; }
        public List<FieldMapping> FieldMappings { get; set; }
        public FwDataSourceInfo FwDataSourceInfo { get; set; }

        [XmlIgnore] public bool SkipLoadingBecauseOfProblem { get; set; }
        [XmlIgnore] public DateTime LastModification { get; set; }
        [XmlIgnore] public bool IsSfmType { get; }
        [XmlIgnore] public string TypeAsString { get; }
        [XmlIgnore] public bool FwSourceDirectFromDB { get; }
        public string DisplayTextWhenReading { get; }
    }
}
```

**`PaDataSource(fields, filename)` behaviour** — this is what makes the
generated SFM file self-typing:

1. `.wav` ⇒ `Type = SA`.
2. Otherwise try `XmlDocument.Load`. Parses ⇒ PAXML/XML type.
3. Otherwise SFM detection: first line starts with `\_sh ` ⇒ `Toolbox`;
   else ≥60 % of lines start with `\` ⇒ `SFM`. Then
   `FieldMappings = CreateDefaultSfmMappings(fields)` and `SfmRecordMarker` is
   chosen from the `DefaultSfmRecordMarker` setting (value: `\ref`).

## `SIL.Pa.Model` (Pa.exe)

```csharp
namespace SIL.Pa.Model
{
    public enum FieldType
    {
        GeneralText, GeneralNumeric, GeneralFilePath, Date,
        Reference, Phonetic, AudioFilePath
    }

    [XmlType("field")]
    public class PaField
    {
        public const string kPhoneticFieldName      = "Phonetic";
        public const string kCVPatternFieldName     = "CVPattern";
        public const string kDataSourceFieldName    = "DataSource";
        public const string kDataSourcePathFieldName= "DataSourcePath";
        public const string kAudioFileFieldName     = "AudioFile";
        public const string kPhoneticSourceFieldName= "Phonetic Source";

        public PaField();
        public PaField(string name);
        public PaField(string name, FieldType type);

        public string Name { get; set; }
        public FieldType Type { get; set; }
        public bool IsCollection { get; }
        public string[] GetPossibleDataSourceFieldNames();
        public PaField Copy();
    }

    [XmlType("mapping")]
    public class FieldMapping
    {
        public FieldMapping();
        public FieldMapping(PaField field, bool isParsed);
        public FieldMapping(PaField field, string parsedFields);
        public FieldMapping(PaField field, IEnumerable<string> parsedFields);
        public FieldMapping(string nameInSource, PaField field, bool isParsed);  // ← the one we use

        [XmlAttribute("nameInSource")] public string NameInDataSource { get; set; }
        [XmlElement("paFieldName")]    public string PaFieldName { get; set; }
        [XmlElement("isParsed")]       public bool IsParsed { get; set; }
        [XmlElement("isInterlinear")]  public bool IsInterlinear { get; set; }
        [XmlElement("fwWritingSystem")] public string FwWsId { get; set; }
        [XmlIgnore]                    public PaField Field { get; set; }
        public FieldMapping Copy();
    }

    public class PaProject : IDisposable
    {
        public List<PaDataSource> DataSources { get; set; }        // public setter
        public IEnumerable<PaField> Fields { get; private set; }
        public string Folder { get; }                               // Path.GetDirectoryName(_fileName)
        [XmlElement("name")] public string Name { get; set; }
        public string ProjectPathFilePrefix { get; }
        public void Save();
        public void ReloadDataSources();
        public void CheckForModifiedDataSources();
    }
}
```

### PA's standard field names

From `DistFiles/Configuration/DefaultFields.xml`. Only fields with
`possibleDataSourceFieldNames` can be auto-mapped by PA from SFM markers; the
rest must be mapped programmatically (which the add-on does).

| PA field | Type | SFM markers PA recognises |
|---|---|---|
| `Reference` | Reference | `\ref` |
| `Phonetic` | Phonetic | `\ph` `\tx` |
| `Gloss` | GeneralText | `\gl` `\ge` |
| `Gloss-Secondary` | GeneralText | `\gn` |
| `Gloss-Other` | GeneralText | — |
| `PartOfSpeech` | GeneralText | `\ps` |
| `Tone` | GeneralText | `\tn` `\pi` |
| `Orthographic` | GeneralText | **none** |
| `Phonemic` | GeneralText | **none** |
| `Note` | GeneralText | **none** |
| `AudioFile` | AudioFilePath (collection) | `\sf` `\snd` |

Relevant application settings (`src/Pa/Properties/Settings.settings`):

- `DefaultSfmRecordMarker` = `\ref` (single value — PA uses `SingleOrDefault`
  over the split list, so multiple matches would throw)
- `DefaultParsedSfmFields` =
  `Phonetic;Phonemic;Gloss;Gloss-Secondary;Gloss-Other;PartOfSpeech;Tone;Orthographic`
- `ReloadProjectsWhenAppBecomesActivate` = `True` (drives auto-refresh)

---

## Building a stub assembly (offline type-checking)

Without `Pa.exe` you cannot compile `src/PaDekereke`. You *can* type-check it
by declaring the surface above in a stub project and referencing that instead:

- One net48 class library, e.g. `src/PaStubs/PaStubs.csproj`, with
  `<AssemblyName>Pa</AssemblyName>`, plus a second with
  `<AssemblyName>SilTools</AssemblyName>` (the reference names must match).
- Members can throw `NotImplementedException`; only signatures matter.
- Wire it up behind an MSBuild condition so a real PA install still wins:
  ```xml
  <ItemGroup Condition="'$(UseStubs)' == 'true'">
    <ProjectReference Include="..\PaStubs\PaStubs.csproj" />
  </ItemGroup>
  ```
  and guard the existing `<Reference>` items with the inverse condition.

**This stub must never be shipped or installed** — it exists only to prove the
add-on compiles. A green stub build is *not* evidence the add-on works against
real PA; say so explicitly when reporting results.

Implemented as `src/PaStubs` + `src/SilToolsStubs`; `src/PaDekereke` builds
against them with `dotnet build -p:UseStubs=true`.

**Field confirmation (2026-08-10):** a stub-compiled `PaDekereke.dll` was
loaded by a real installed PA 4.1.1 - the add-on loader instantiated
`PaAddOnManager`, the references bound to the installed `Pa.exe`/`SilTools.dll`
at run time, and `App.AddMediatorColleague(IxCoreColleague)` executed. So
stub-built binaries are not merely type-checked: they bind against the shipped
PA (no strong-naming in the way). Later the same day, opening a project
confirmed the dispatch convention live: the mediator invoked the add-on's
`protected OnBeforeLoadingDataSources(object)` by name, and the handler read
`PaProject.Name` and `PaProject.DataSources.Count` off the live project (a
FieldWorks-sourced one, traversed as a clean no-op). Run-time correctness of
the remaining surface (swap/restore, `FieldMapping`, `PaDataSource` ctor
behaviour) still needs the Dekereke load itself.

### Types referenced above but not declared (stub-building gap, found 2026-08)

Two types appear in the declarations above without their own transcription, so
a stub built "exactly from this document" does not compile until they are
declared somewhere:

- **`ITMAdapter`** — the type of `App.TMAdapter`. Lives in `SilTools.dll`; the
  add-on never touches it. The stub declares an empty
  `SilTools.ITMAdapter` placeholder. Its real (large) declaration and exact
  namespace were not verified against PA source when this file was written.
- **`FwDataSourceInfo`** — used by a `PaDataSource` constructor and property;
  FieldWorks-only, never touched by the add-on. The stub declares an empty
  placeholder in `SIL.Pa.DataSource`; the real type's exact namespace (possibly
  a `FieldWorks` sub-namespace) was not verified offline.

Neither gap affects the add-on itself — it uses neither type — but anyone
regenerating the stubs, or extending the add-on to touch toolbars/menus
(`App.TMAdapter`) or FieldWorks sources, must transcribe the real declarations
from PA source first.
