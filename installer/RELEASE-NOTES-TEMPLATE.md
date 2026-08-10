Use a Dekereke phonology database as a **live** Phonology Assistant data
source: add the `.xml` once, then edit in Dekereke and switch back to PA —
it reloads by itself, no re-import, no converter to re-run.

## Install

Download **PaDekereke-Setup-*.exe** below and run it. It finds Phonology
Assistant, installs into its `AddOns` folder, and adds an uninstall entry.
Prefer to do it by hand? Use the `-manual-install.zip` instead — same files,
same administrator prompt (Phonology Assistant lives in Program Files).

**Windows will warn you before it runs.** These builds are not code-signed, so
SmartScreen shows "Windows protected your PC" — click *More info* → *Run
anyway*. Signing certificates cost money this project does not have. Every
release is built in public by GitHub Actions from the source in this
repository, and GitHub lists a SHA-256 for each file below if you want to
check what you downloaded.

Requires Phonology Assistant 4.1.1 on Windows, and a Dekereke database
(<https://casali.canil.ca>).

## Using it

1. **File → New Project** (or **Project Settings** on an open project)
2. **Add ▾ → Dekereke Data Source…** and pick your Dekereke `.xml`
3. Confirm the column mapping — the guesses are usually right — then **OK**

From then on it stays in sync. To change the mapping later, hold **Shift**
while the project loads.

## Notes

- Records with an empty phonetic field are skipped; the count is reported in
  `%LOCALAPPDATA%\PaDekereke\addon.log`, which also logs each refresh.
- Your Dekereke file is never modified. The add-on converts a copy in a
  private cache folder on each load.
- Dekereke column names differ from database to database, so nothing is
  hard-coded: the mapping is guessed from your own column names and is
  yours to adjust.
