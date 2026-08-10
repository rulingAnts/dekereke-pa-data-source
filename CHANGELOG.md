# Changelog

All notable changes to this project are documented here. Format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versioning follows
[Semantic Versioning](https://semver.org/).

## [1.0.0] — 2026-08-10

**Working end to end against a live Phonology Assistant 4.1.1 install**
(2026-08-10): a real ~1000-record Dekereke database was added through PA's own
New Project dialog, mapped once, loaded (944 records; 122 skipped for having no
phonetic form), and thereafter **auto-refreshed on every edit** — the goal of
the whole project. Details and log evidence in `HANDOFF.md`.

### Added since the first draft
- **"Dekereke Data Source…" in PA's Add dropdown**, in the New Project dialog
  and Project Settings alike — the discoverable route a normal user needs. That
  dropdown is plain WinForms and not extensible by PA's add-on menu API, so it
  is injected at runtime; the private members that depends on are tabulated in
  `docs/pa-internals/api-surface.md`.
- **Tools → "Add Dekereke Data Source…"** for a project that is already open,
  as a proper `ITMAdapter` menu item.
- The column mapping is now confirmed **at import**, and remembered, so exactly
  one dialog appears per database.
- File-type validation: only XML whose root element is `phon_data` is accepted.
- Diagnostic log at `%LOCALAPPDATA%\PaDekereke\addon.log` (PA swallows all
  add-on errors, so this is the only way to see what happened).
- Compile-only PA/SilTools API stubs, so the add-on builds with no PA install;
  CI builds the add-on and the Windows installer on every push.
- `DekerekeConvert` CLI (Dekereke → Toolbox SFM snapshot), for diagnostics.
- Mapping dialog: column filter box and remembered window size.
- User-facing download site in `docs/`.

### Fixed since the first draft
- Add-on registers only its first instance per process, keeps per-load state
  re-entrancy-safe, and never stacks a second mapping dialog.
- Data sources are handed to PA fully formed: PA's project settings dialog
  rejects an XML source with no XSLT file and no phonetic field mapping, and
  crashes on a mapping whose field is unresolved. All three are handled.
- Installer's PA detection implemented via the MSI UpgradeCode, with a
  path-probe fallback; the script now actually compiles (verified in CI).

### Added
- Core library `DekerekeToPa` (netstandard2.0, no PA dependency):
  - Dekereke XML reader handling every encoding variant in the field —
    UTF-16LE+BOM (older releases), UTF-8+BOM, and plain UTF-8 with no BOM
    (current release) — by handing the raw stream to the XML parser.
  - Per-database column auto-mapper with English and Indonesian heuristics
    (Dekereke column names are user-defined, so mappings cannot be hard-coded).
  - Toolbox SFM writer honoring every constraint of PA's `SfmDataSourceReader`
    (newline flattening, no duplicate markers per record, synthesized `\ref`
    when empty, `\_sh` header, UTF-8 BOM output).
  - Mapping persistence and a conversion cache under `%LOCALAPPDATA%`.
- NUnit test suite (net8.0, runs on any OS) with runtime-generated encoding
  fixtures.
- Phonology Assistant add-on `PaDekereke` (net48): hooks PA's
  `BeforeLoadingDataSources`/`AfterLoadingDataSources` broadcasts to swap
  Dekereke sources for generated SFM during each load and restore afterwards,
  so the project keeps pointing at the Dekereke file and PA's own change
  detection drives auto-refresh. One-time column-mapping dialog;
  Shift-on-load to remap.
- Inno Setup installer script (PA detection via MSI UpgradeCode still stubbed).
- Anonymized sample Dekereke databases in the old and current formats.
- `HANDOFF.md` (verified PA internals, traps, work list, Windows verification
  checklist), `CLOUD_PROMPT.md`, `docs/PLAN.md`, `LICENSING.md`.

### Known open items
- The installer has been **compiled** but not yet **run** on Windows: PA
  detection via `MsiEnumRelatedProducts` is unverified against a real PA
  install, as is the uninstall path. The path-probe fallback and manual browse
  cover a detection miss.
- The add-on binary is compiled against reconstructed API stubs rather than a
  real `Pa.exe`. It binds and runs correctly against PA 4.1.1 (confirmed
  live), but a future PA could diverge.
- The project-settings dropdown integration binds to PA private members by
  name; a future PA release could rename them. It fails soft (the menu item
  simply does not appear, with a reason logged).
- The AGPL license blocks the upstream-contribution track — see
  `LICENSING.md`.
- Part B (native `DataSourceType.Dekereke` in PA itself) remains a design only.
