# Sample Dekereke databases

Anonymized test data. The **structure** mirrors two real Dekereke databases
(column inventories, element ordering, empty-element style, pitch notation,
nested acoustic blocks, encodings, CRLF line endings); every **content** item —
phonetic forms, glosses, speaker-named columns — is invented.

| File | Mirrors | Encoding | Notes |
|---|---|---|---|
| `SampleLang_full.xml` | A large elicitation database (~40 columns) | **UTF-16LE + BOM**, `encoding="utf-16"` declaration | What current Dekereke writes. Includes verb-paradigm columns with `_Pitch` twins, elicitation-frame columns (`goodX`, `Xbad`, …), columns with `-` and `.` in their names (`IMP-re`, `Orth.practice`), a record with empty `Phonetic` (must be skipped on conversion), and `<qvp_acoustic_data_>` nested blocks (must be ignored). |
| `SampleLang_full-DkUserSettings.xml` | Its sibling per-user settings file | UTF-16LE + BOM | Carries `<sound_file_path>` — Dekereke stores bare `.wav` names in the database and the audio folder here. |
| `SampleLang_minimal.xml` | A small orthography-checking database (Indonesian UI: `Tulisan`, `Catatan`, `kosong`) | **UTF-8 + BOM** | What newer Dekereke versions write. Deliberately shares only a handful of columns with the full sample — column inventories are user-defined per database, and any importer must cope with both. |

Encoding matters here: these files exist precisely because the reader must
handle UTF-16LE-with-BOM (with a matching `utf-16` declaration) and UTF-8
transparently. Do not "clean up" or re-save them with an editor.
