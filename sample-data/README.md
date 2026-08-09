# Sample Dekereke databases

Anonymized test data. The **structure** mirrors two real Dekereke databases
(column inventories, element ordering, empty-element style, pitch notation,
nested acoustic blocks, encodings, CRLF line endings); every **content** item —
phonetic forms, glosses, speaker-named columns — is invented.

## Encodings

Dekereke's on-disk encoding changed over time. Nothing else about the format
changed, so a reader must treat these as interchangeable:

| Variant | Where it comes from |
|---|---|
| **UTF-16LE + BOM**, `encoding="utf-16"` declaration | Older Dekereke releases — still very much in the field (Seth's own working database is one) |
| UTF-8 + BOM | Intermediate |
| **Plain UTF-8, no BOM** | Current Dekereke release (Rod Casali changed the output encoding in 2026, at Seth's request) |

The reader must therefore hand the raw byte stream to the XML parser and let it
resolve the encoding from BOM and declaration. Guessing, or pre-decoding to a
string, breaks at least one of these variants — see `HANDOFF.md`.

## Files

| File | Mirrors | Encoding | Notes |
|---|---|---|---|
| `SampleLang_full.xml` | A large elicitation database (~40 columns) | **UTF-16LE + BOM** (older releases) | Verb-paradigm columns with `_Pitch` twins, elicitation-frame columns (`goodX`, `Xbad`, …), columns with `-` and `.` in their names (`IMP-re`, `Orth.practice`), a record with empty `Phonetic` (must be skipped on conversion), and `<qvp_acoustic_data_>` nested blocks (must be ignored). |
| `SampleLang_full-DkUserSettings.xml` | Its sibling per-user settings file | UTF-16LE + BOM | Carries `<sound_file_path>` — Dekereke stores bare `.wav` names in the database and the audio folder here. |
| `SampleLang_minimal.xml` | A small orthography-checking database (Indonesian UI: `Tulisan`, `Catatan`, `kosong`) | **Plain UTF-8, no BOM** (current release) | Deliberately shares only a handful of columns with the full sample — column inventories are user-defined per database, and any importer must cope with both. |

Do not "clean up" or re-save these files with an editor: their encodings are
the point. Unit tests generate their own fixtures (including the UTF-8 + BOM
and no-declaration variants) rather than reading these, so the two never drift.
