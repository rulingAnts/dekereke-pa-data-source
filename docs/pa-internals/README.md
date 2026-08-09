# Phonology Assistant internals — offline reference

This folder exists so that work can continue **without network access to
GitHub or SIL**. Everything here was verified by reading Phonology Assistant's
own source; if you cannot reach the PA repository, treat these files as the
authority and do not guess.

## Provenance

Source: [`sillsdev/phonology-assistant`](https://github.com/sillsdev/phonology-assistant)
@ `master`, corresponding to **PA 4.1.1** (released 2025-02-19; .NET Framework
4.8, WinForms, x86). Phonology Assistant is © SIL International and
**MIT-licensed**. The short excerpts quoted in these files are reproduced for
interoperability documentation, with attribution, under that license. No PA
code is compiled into, linked from, or redistributed by this project — the
add-on is loaded *by* PA at run time and references its assemblies externally.

## Files

| File | Purpose |
|---|---|
| `api-surface.md` | Exact signatures of every PA type and member the add-on touches — enough to write a compilable stub assembly without `Pa.exe` |
| `hook-points.md` | The load pipeline, add-on loader, and change-detection chain, with the relevant code quoted |

`../../HANDOFF.md` is the narrative version, including the traps. Read it first.

## If something here is wrong

Say so loudly in your summary rather than silently "fixing" the code around it.
A discrepancy between this reference and reality is the single most valuable
thing an offline session can discover, because everything downstream was built
on it.
