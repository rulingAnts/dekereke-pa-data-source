# Changelog

All notable changes to this project are documented here. Format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versioning follows
[Semantic Versioning](https://semver.org/).

## [Unreleased]

Nothing has been compiled or run yet — see `HANDOFF.md` for the exact state and
the remaining work. Nothing below is verified against a live Phonology
Assistant install.

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
- Nothing compiled; core tests not yet run.
- Installer's MSI-based PA detection is a stub with a path-probe fallback.
- The AGPL license blocks the upstream-contribution track — see
  `LICENSING.md`.
