# Prompt for the cloud session

Paste everything below the line into a Claude Code cloud session pointed at
`rulingAnts/dekereke-pa-data-source`.

Written on the assumption that the session **cannot reach github.com or
software.sil.org**. Everything needed is committed in the repo. If the session
*does* have network access, it can additionally clone
`sillsdev/phonology-assistant` (public, MIT) to double-check the reference
docs — but it must never push there or open PRs.

---

## Read these first, in this order

1. `HANDOFF.md` — the engineering briefing: how the add-on hooks Phonology
   Assistant, and six traps that each cost real investigation. Do not
   re-derive or contradict it.
2. `docs/pa-internals/api-surface.md` — exact signatures of every PA type and
   member the add-on uses (PA's assemblies are **not** in this repo).
3. `docs/pa-internals/hook-points.md` — the PA source excerpts those claims
   rest on, with file:line provenance.
4. `sample-data/README.md` — the three Dekereke on-disk encodings and what each
   sample file exercises.
5. `docs/PLAN.md` — full design rationale. Skim; Part B is out of scope.

`CONTRIBUTING.md` has the per-project constraints (target frameworks, C# 7.3,
style). `LICENSING.md` has an unresolved licensing question — **do not resolve
it**, it is the owner's decision.

## What this project is

A Phonology Assistant add-on that makes a Dekereke phonology database work as a
**live, auto-refreshing** PA data source. Dekereke is Rod Casali's Phonology
Database software (casali.canil.ca); its files are XML with **user-defined
column names that differ per database**, which is why nothing about the mapping
can be hard-coded.

The user experience being built: install PA → run our installer → add a
Dekereke `.xml` as a data source → confirm a guessed column mapping once → edit
in Dekereke, switch to PA, PA reloads by itself.

## Tasks, in strict priority order

### 1. Make the core tests green — the main deliverable

```bash
dotnet test src/DekerekeToPa.Tests
```

`src/DekerekeToPa` and its tests have **never been compiled** (they were
authored on a machine with no .NET SDK). Expect mechanical fixes. This is
fully achievable offline: the core library has no PA, Windows or network
dependency, and the tests generate their own fixtures.

Rules while fixing:
- Keep `src/DekerekeToPa` netstandard2.0, C# 7.3, free of PA/Windows/WinForms.
- The encoding behaviour is load-bearing and non-negotiable — Dekereke files
  exist as UTF-16LE+BOM (older releases), UTF-8+BOM, and plain UTF-8 with no
  BOM (current release, changed in 2026). Always feed the **raw stream** to the
  XML parser; never `File.ReadAllText` first, never require a BOM. If a test
  seems to contradict this, the test is wrong.
- The SFM writer's constraints come from PA's reader (`hook-points.md` §5) —
  no raw newlines in values, no repeated marker within a record, never a bare
  `\ref`. Do not relax these to make a test pass.
- Never re-save anything in `sample-data/`; the encodings are the point.

### 2. Prove the add-on compiles, using stubs

`src/PaDekereke` references the installed `Pa.exe` and `SilTools.dll`, which
you do not have and cannot download. Build a **stub assembly** instead — the
recipe is at the end of `docs/pa-internals/api-surface.md`: two net48 class
libraries named `Pa` and `SilTools` declaring exactly the surface in that
document, members throwing `NotImplementedException`, wired behind an MSBuild
`UseStubs` condition so a real PA install still takes precedence.

Then `dotnet build src/PaDekereke -p:UseStubs=true` and fix what the compiler
finds.

Treat the PA-facing logic in `PaAddOnManager.cs` as deliberate: every call it
makes was verified against PA's source. If the compiler objects, first check
`api-surface.md` — a mismatch there is a finding worth reporting loudly, not
something to paper over by changing the add-on's behaviour.

**A green stub build is not evidence the add-on works.** Say so in your summary.

### 3. CI

Extend `.github/workflows/tests.yml`: the ubuntu job running the core tests
should be green. Add a job that builds `src/PaDekereke` against the stubs so
compile breakage is caught. Do not add a job that downloads PA.

### 4. Installer

`installer/PaDekereke.iss` has a stubbed PA-detection routine with a `TODO`
comment giving the API and the UpgradeCode
(`{5E57E4D4-580A-4cc1-9E0C-7EF8D3F81BBD}`, stable across PA versions).
Implement it. You cannot run Inno Setup here; get it correct by inspection and
mark it unverified.

### 5. Only if everything above is done

Core-library edge cases you discover; `MappingDialog` polish (column filter
box, remembered window size).

## Hard constraints

- **Do not attempt the Windows verification checklist** at the end of
  `HANDOFF.md`. It needs PA installed on Windows and belongs to a human. Never
  report those steps as passing.
- **Out of scope:** Part B (patching PA itself), and anything requiring the
  real `Pa.exe`.
- Commit in small, clearly described steps to this repository only.

## Finish with

A summary covering: what is green; what compiled and against what (stubs vs.
real PA); what remains; every item you could **not** verify; and — most
important — any place where reality disagreed with `HANDOFF.md` or
`docs/pa-internals/`. Flag those loudly. Those docs are the foundation
everything else was built on, so a single wrong line in them is worth more than
a dozen green tests.
