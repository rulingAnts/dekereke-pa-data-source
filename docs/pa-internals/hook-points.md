# PA hook points — quoted source

Short excerpts from Phonology Assistant 4.1.1 (© SIL International, MIT),
reproduced for interoperability documentation. Line numbers are from
`sillsdev/phonology-assistant` @ `master`.

---

## 1. The add-on loader — `src/Pa/App.cs:367`

Called from `PaMainWnd`'s constructor (`src/Pa/UI/PaMainWnd.cs:80`), i.e.
**before any project is opened**.

```csharp
/// Add Ons are undocumented and it's assumed that each add-on assembly contains at
/// least a class called "PaAddOnManager". If a class by that name is not found in
/// an assembly in the AddOns folder, then it's not considered to be an AddOn
/// assembly for PA. It's up to the PaAddOnManager class in each add-on to do all
/// the proper initialization it needs. There's nothing in the PA code that recognizes
/// AddOns. It's all up to the Add On to reference the PA code, not the other way
/// around. So, if an Add On needs to add a menu to the main menu, it's up to the
/// add on to do it.
public static void ReadAddOns()
{
    if (DesignMode) return;

    var addOnPath = Path.Combine(AssemblyPath, "AddOns");
    if (!Directory.Exists(addOnPath)) return;

    addOnAssemblyFiles = Directory.GetFiles(addOnPath, "*.dll");
    if (addOnAssemblyFiles.Length == 0) return;

    foreach (string filename in addOnAssemblyFiles)
    {
        try
        {
            Assembly assembly = ReflectionHelper.LoadAssembly(filename);
            if (assembly != null)
            {
                object instance =
                    ReflectionHelper.CreateClassInstance(assembly, "PaAddOnManager");
                ...
            }
        }
        catch { }
    }
}
```

Consequences: the class **must** be named exactly `PaAddOnManager`, be public,
and have a parameterless constructor that does all registration. Every failure
is swallowed — a broken add-on is invisible, not loud. Debug by checking whether
your constructor ran at all.

## 2. The load pipeline — `src/Pa/Model/PaProject.cs:530`

```csharp
private void LoadDataSources()
{
    LoadAmbiguousSequences();
    LoadTranscriptionChanges();
    PhoneticParser = new PhoneticParser(AmbiguousSequences, TranscriptionChanges);

    App.MsgMediator.SendMessage("BeforeLoadingDataSources", this);   // ← convert + swap here
    var reader = new DataSourceReader(this);
    RecordCache = reader.Read();

    var msg = LocalizationManager.GetString(...);
    App.InitializeProgressBar(msg, RecordCache.Count);
    RecordCache.BuildWordCache(App.ProgressBar);                     // ← can save the .pap (trap 2)
    PhoneticParser.LogUndefinedCharactersWhenParsing = false;
    App.IncProgressBar();
    TempRecordCache.Save();
    App.UninitializeProgressBar();

    EnsureSortOptionsValid();
    App.MsgMediator.SendMessage("AfterLoadingDataSources", this);    // ← restore here
}
```

The argument passed to both messages is the `PaProject` itself, whose
`DataSources` list is publicly mutable — that gap is the entire mechanism.

## 3. Auto-refresh — `src/Pa/Model/PaProject.cs:484`

```csharp
private void HandleApplicationWindowActivated(object sender, EventArgs e)
{
    if (Properties.Settings.Default.ReloadProjectsWhenAppBecomesActivate)
        CheckForModifiedDataSources();
}

public void CheckForModifiedDataSources()
{
    if (_reloadingProjectInProcess) return;
    if (Utils.MessageBoxJustShown) { Utils.MessageBoxJustShown = false; return; }

    if (DataSources.Any(ds => !ds.SkipLoadingBecauseOfProblem && ds.UpdateLastModifiedTime()))
        App.MsgMediator.PostMessage("ReloadProject", null);
}
```

`UpdateLastModifiedTime()` compares `File.GetLastWriteTimeUtc(SourceFile)` with
`ds.LastModification` and updates it when newer. **This is why the project's
data source must keep pointing at the Dekereke file.** Point it at a generated
file and PA watches the generated file — a snapshot converter with extra steps.

## 4. The mid-load project save — `src/Pa/Processing/ProjectInventoryBuilder.cs:221`

Reached via `RecordCache.BuildWordCache` → `ProjectInventoryBuilder.Process`,
**inside** the swap window:

```csharp
if (!File.Exists(_project.CssFileName.Replace(".css", ".PhoneticInventory.xml")))
{
    _project.IgnoredSymbolsInCVCharts = new List<string> {"̩"};
    _project.Save();          // ← writes the .pap while our temp source is installed
}
```

Guarded by a file-existence check, so it fires on the **first load of a new
project**. Mitigation: unconditional `project.Save()` after restoring in
`OnAfterLoadingDataSources`.

## 5. SFM reading — `src/Pa/DataSourceClasses/SfmDataSourceReader.cs:72`

```csharp
foreach (var line in File.ReadAllLines(m_dataSource.SourceFile))
{
    var currLine = line.Trim();

    // Toss out lines that don't begin with a backslash or that precede
    // the first line in the file that begins with our record marker.
    if (!currLine.StartsWith("\\") ||
        (!foundFirstRecord && !currLine.StartsWith(recMrkr, StringComparison.Ordinal)))
        continue;

    foundFirstRecord = true;

    if (currLine.StartsWith(recMrkr, StringComparison.Ordinal) && recordLines.Count > 0)
    {
        recCache.Add(SaveSingleRecord(recordLines));
        recordLines.Clear();
    }

    var split = currLine.Split(" ".ToCharArray(), 2);
    if (split.Length >= 2)
        recordLines[split[0]] = split[1].TrimStart();
}
```

Note `recMrkr` has a trailing space appended (`recMarker += " "` at `:60`).
Everything the SFM writer must respect follows from this loop:

- non-`\` lines vanish ⇒ **no raw newlines inside values**
- `recordLines` is a `Dictionary` ⇒ **no repeated marker within a record**
- `split.Length >= 2` ⇒ a marker line with no value is dropped; for the record
  marker that silently **merges two records** ⇒ never emit a bare `\ref`
- `File.ReadAllLines` ⇒ UTF-8 (BOM-detecting); write UTF-8, never UTF-16

Also `DataSourceReader.cs:287` calls `ds.UpdateLastModifiedTime()` after a
successful read — which stamps the *temp* file's mtime during a swap (trap 3).

## 6. The dormant XSLT path (context, not used by the add-on)

PA has an unfinished generic XML-import feature: `DataSourceType.XML`,
`PaDataSource.XSLTFile` (persisted in the `.pap`), a "Specify XSLT" file picker
and validation — all present, with the reader stubbed at
`DataSourceReader.cs:274` (`case DataSourceType.XML: break;`) and the UI column
hidden at `ProjectSettingsDlg.cs:227`:

```csharp
// When xslt transforms are supported when reading data, then this should become visible.
m_grid.Columns["xslt"].Visible = false;
```

Finishing it is the alternative upstream route discussed in `docs/PLAN.md`; it
is *not* what this add-on uses, because it would still require every user to
hand-author an XSLT for their own column names.
