# Contributing

## Licensing

Contributions are accepted under **AGPL-3.0-or-later**. Read
[LICENSING.md](LICENSING.md) first — there is an unresolved decision about
dual-licensing `src/DekerekeToPa`, and it needs settling before outside
contributions accumulate (retroactive relicensing needs everyone's agreement).

Add the SPDX header from any existing source file to new `.cs` files.

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
