# Prompt for the cloud session

Copy everything below the line into a Claude Code cloud session pointed at
`rulingAnts/dekereke-pa-data-source`.

---

You are continuing a designed-and-scaffolded project. **Read `HANDOFF.md` first,
then skim `docs/PLAN.md`** — they contain verified facts about Phonology
Assistant's internals (with file:line references) that must not be re-derived
or contradicted. The PA repo is `sillsdev/phonology-assistant` on GitHub
(public, MIT) — clone it read-only whenever you need to check a claim against
its source. Do not open PRs against it and do not push anywhere except this
repository.

Goal of this session, in strict priority order:

1. **Make the core tests green.** `dotnet test src/DekerekeToPa.Tests`.
   The code was authored without a compiler; expect small mechanical fixes.
   Keep `src/DekerekeToPa` netstandard2.0 / C# 7.3-compatible and free of any
   PA or Windows dependency. The encoding behavior described in HANDOFF.md
   ("The traps", item 1) is load-bearing — if a test contradicts it, the test
   is wrong, not the trap.
2. **Compile the add-on.** `src/PaDekereke` needs `Pa.exe` and `SilTools.dll`;
   HANDOFF.md ("Getting PA assemblies without a PA install") explains how to
   obtain them from the PA installer with msitools/7z. Fix what the compiler
   finds, but treat the PA-facing logic in `PaAddOnManager.cs` as
   deliberate — every call it makes is against an API verified in the PA
   source; re-read the referenced PA lines before changing any of it.
3. **CI.** Extend `.github/workflows/tests.yml`: the ubuntu test job should be
   green; add a windows job that downloads/extracts PA and builds the add-on,
   uploading `PaDekereke.dll` + `DekerekeToPa.dll` as an artifact.
4. **Installer.** Implement the MSI UpgradeCode detection stubbed in
   `installer/PaDekereke.iss` (code comments inside give the API and the GUID).
5. Only if all the above is done: improve `MappingDialog` (column search box,
   remember window size) and add core-library tests for edge cases you find.

Constraints:
- Windows-only verification steps (the checklist in HANDOFF.md) are for a
  human with a Windows VM — do not attempt them, do not fake their results.
  Anything you could not verify must be listed as unverified in your summary.
- `sample-data/` files have deliberate encodings (UTF-16LE+BOM, UTF-8+BOM,
  CRLF). Never re-save or "fix" them; tests generate their own fixtures.
- Commit in small, described steps to this repository.
- Part B (patching PA itself) is out of scope for this session.

Finish with a summary of: what is green, what compiled, what remains, and any
place where reality disagreed with HANDOFF.md (flag those loudly).
