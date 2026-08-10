# Contributing

## Licensing

Contributions are accepted under **AGPL-3.0-or-later** — except under
`src/DekerekeToPa/`, which is **dual-licensed AGPL-3.0-or-later OR MIT**
(settled 2026-08-10 so the core library can be contributed upstream to
Phonology Assistant; see [LICENSING.md](LICENSING.md)). Contributions to that
directory must be offered under both licences; pull requests touching it
should say so explicitly.

Add the SPDX header from an existing source file *in the same directory* to
new `.cs` files — the header differs between the dual-licensed core and the
rest.

## Before you change anything

Read [HANDOFF.md](HANDOFF.md). It records facts about Phonology Assistant's
internals verified against its source, with `file:line` references — including
several traps that cost real investigation. If your change contradicts
something there, check PA's source
([sillsdev/phonology-assistant](https://github.com/sillsdev/phonology-assistant),
MIT) before assuming HANDOFF is wrong — and if it *is* wrong, fix HANDOFF in
the same commit.

## Layout and constraints

| Project | Target | Rules |
|---|---|---|
| `src/DekerekeToPa` | netstandard2.0 | No PA dependency, no Windows dependency, no WinForms. C# 7.3. This is the portable core and the part that could go upstream. |
| `src/DekerekeToPa.Tests` | net8.0 | Runs anywhere via `dotnet test`. Generates its own fixtures. |
| `src/PaDekereke` | net48 | References the installed `Pa.exe`/`SilTools.dll`. C# 7.3. Windows-only at run time. |

Style: tabs, `m_`/`_` field prefixes, XML doc comments on public members —
matching the PA codebase, since part of this may end up there.

## Testing

```bash
dotnet test src/DekerekeToPa.Tests
```

Anything touching the reader must keep
`Read_AllEncodingVariants_YieldIdenticalContent` green: Dekereke files exist as
UTF-16LE+BOM (older releases), UTF-8+BOM, and plain UTF-8 with no BOM (current
release), and all must parse identically.

Never re-save the files in `sample-data/` — their encodings and CRLF endings
are deliberate. Tests generate their own fixtures precisely so the samples can
stay untouched.

## Things that need a Windows machine

The verification checklist at the end of HANDOFF.md requires PA installed on
Windows. Don't claim those steps pass unless you actually ran them; mark
unverified work as unverified.
