# Dekereke Data Sources for Phonology Assistant

[![License: AGPL v3](https://img.shields.io/badge/License-AGPL_v3-blue.svg)](LICENSE)
[![tests](https://github.com/rulingAnts/dekereke-pa-data-source/actions/workflows/tests.yml/badge.svg)](https://github.com/rulingAnts/dekereke-pa-data-source/actions/workflows/tests.yml)

Use a [Dekereke](https://casali.canil.ca/) phonology database (Rod Casali's
Phonology Database software) as a **live, auto-refreshing data source** in SIL's
[Phonology Assistant](https://software.sil.org/phonologyassistant/) (PA).

For the user, the experience is:

1. Install Phonology Assistant.
2. Run this add-on's installer.
3. In PA, add your Dekereke `.xml` database as a data source.
4. Confirm the suggested column→field mapping once (it is guessed from your
   actual column names).
5. Work. Edit in Dekereke, save, switch to PA — PA reloads by itself.

No XSLT, no config files, no hand-conversion, no snapshots going stale.

## Why this is not just a converter

A converter produces a snapshot: PA shows stale data until you re-run it.
This add-on hooks PA's own load pipeline, so **the data source PA watches is
the Dekereke file itself**. PA's built-in change detection (on by default)
notices a Dekereke save whenever PA regains focus and re-runs the load — and
the add-on re-converts transparently inside that load, every time.

## How it works

Phonology Assistant scans `<install>\AddOns\*.dll` at startup and instantiates
any class named `PaAddOnManager`. This add-on listens for PA's
`BeforeLoadingDataSources` / `AfterLoadingDataSources` broadcasts and, in the
gap between them, temporarily swaps each Dekereke data source for a generated,
fully field-mapped Toolbox SFM file (PA's most mature import format), restoring
the original immediately after the read. Full mechanism, with PA source line
references: [HANDOFF.md](HANDOFF.md).

Dekereke column names are user-defined per database, so mappings can't be
hard-coded: the add-on reads the actual column names from your file, guesses
the mapping (English and Indonesian column-name heuristics), and lets you
adjust it in a small dialog — once. Hold **Shift** while PA loads the project
to reopen the mapping dialog.

## Repository layout

| Path | What |
|---|---|
| `src/DekerekeToPa/` | Core library (netstandard2.0, no PA dependency): read Dekereke XML (every encoding variant), auto-map columns, write Toolbox SFM |
| `src/DekerekeToPa.Tests/` | NUnit tests (net8.0 — run anywhere with `dotnet test`) |
| `src/PaDekereke/` | The PA add-on (net48; references the installed `Pa.exe`/`SilTools.dll`) |
| `installer/` | Inno Setup script |
| `sample-data/` | Anonymized sample Dekereke databases (old UTF-16LE and current UTF-8 formats) |
| `HANDOFF.md` | Technical deep-dive: every PA hook point with file:line, traps, verification plan |
| `docs/PLAN.md` | The full design document |
| `CONTRIBUTING.md` | Layout rules, style, testing, what needs Windows |
| `LICENSING.md` | Third-party licenses and one open licensing decision |

## Building

```bash
dotnet test src/DekerekeToPa.Tests          # core library — any OS
dotnet build src/PaDekereke -c Release      # add-on — needs Pa.exe/SilTools.dll (see HANDOFF.md)
```

The add-on must be compiled against Phonology Assistant 4.x assemblies
(default: `C:\Program Files (x86)\SIL\Phonology Assistant\`; override with
`-p:PaInstallDir=...`). Install = copy the build output into PA's `AddOns\`
folder, or run the installer.

## Status

Core library and tests written; add-on written pending compile/verification
against a live PA install; installer drafted. See HANDOFF.md for the precise
state and the remaining work items. A second track — native Dekereke support
contributed to PA itself — is designed in `docs/PLAN.md` (Part B).

## License

**AGPL-3.0-or-later** — see [LICENSE](LICENSE), and [LICENSING.md](LICENSING.md)
for third-party components and one open licensing decision (the AGPL currently
blocks the upstream-contribution track described in `docs/PLAN.md`).

Phonology Assistant is © SIL International, MIT-licensed, at
[sillsdev/phonology-assistant](https://github.com/sillsdev/phonology-assistant).
Dekereke / Phonology Database is Rod Casali's software
([casali.canil.ca](https://casali.canil.ca/)). This project is unaffiliated
with either and only makes them talk to each other — it reads Dekereke's file
format and uses no Dekereke code.
