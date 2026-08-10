# Licensing

This project is licensed under the **GNU Affero General Public License v3.0 or
later** (AGPL-3.0-or-later), with one deliberate exception: the core library
in `src/DekerekeToPa/` is **dual-licensed AGPL-3.0-or-later OR MIT**, at the
recipient's option. The full texts are in [LICENSE](LICENSE) and
[LICENSE-MIT](LICENSE-MIT); every source file carries an SPDX header saying
which terms apply to it.

Copyright © 2026 Seth Johnston.

## Third-party code

| Component | License | Relationship |
|---|---|---|
| Phonology Assistant (`Pa.exe`, `SilTools.dll`) | MIT, © SIL International | The add-on is compiled against these and loaded by PA at run time. Not redistributed by this project. |
| Dekereke / Phonology Database (Rod Casali, casali.canil.ca) | Proprietary, unaffiliated | Only its **file format** is read. No Dekereke code is used, linked, or redistributed. |
| NUnit, Microsoft.NET.Test.Sdk, Microsoft.NETFramework.ReferenceAssemblies | MIT | Build/test only; not shipped. |

MIT is compatible with the AGPL in this direction — MIT-licensed code may be
incorporated into an AGPL work. The reverse is not true, which matters below.

## ✔ Decided 2026-08-10: option 1, the core library is dual-licensed

The open question below was settled by the copyright holder when preparing the
upstream (Part B) proposal: **option 1 was adopted.** `src/DekerekeToPa/` is
offered under **AGPL-3.0-or-later OR MIT** (SPDX headers updated,
[LICENSE-MIT](LICENSE-MIT) added), so it can be contributed to the
MIT-licensed Phonology Assistant. The add-on (`src/PaDekereke`), the stubs,
the installer, the tests and the docs remain AGPL-3.0-or-later only.

The original analysis is kept below for the record.

## The decision, as it stood open: the AGPL blocks the upstream-contribution track

`docs/PLAN.md` describes two tracks:

- **Part A** — this add-on. AGPL is fine here. (The AGPL's network-service
  clause is inert for a desktop WinForms add-on, so in practice it behaves like
  GPL-3.0.)
- **Part B** — contributing native Dekereke support *into Phonology Assistant
  itself*. **This cannot be done with AGPL code.** PA is MIT-licensed; SIL
  cannot accept an AGPL contribution without effectively relicensing part of
  their project, and they will not. Part B would reuse `src/DekerekeToPa`
  (the reader, auto-mapper and field-name tables) almost verbatim.

So as it stands, the AGPL choice closes off Part B. Three ways forward — this
is the copyright holder's call, and nothing here has been decided:

1. **Dual-license the core library** *(recommended)*. Keep `src/PaDekereke`,
   the installer and the docs AGPL; offer `src/DekerekeToPa` under
   **AGPL-3.0-or-later OR MIT**, at the recipient's option. Copyleft still
   protects the add-on, while the portable part stays contributable upstream.
   Implementation: change the SPDX headers in `src/DekerekeToPa/*.cs` to
   `AGPL-3.0-or-later OR MIT` and note it here.
2. **Relicense Part B at submission time.** As sole copyright holder Seth is
   not bound by his own license and can contribute the same code to PA under
   MIT whenever he chooses. Legally sound, but opaque to anyone reading the
   repo, and it stops working the moment there is a second contributor.
3. **Drop Part B.** AGPL everywhere, add-on only.

If Part B is still wanted, decide before accepting outside contributions —
retroactive relicensing requires every contributor's agreement.

## Contributing

By contributing you agree your contributions are licensed under
AGPL-3.0-or-later — and, for anything under `src/DekerekeToPa/`,
**additionally under MIT** (the directory is dual-licensed and must stay
contributable upstream). Pull requests touching that directory should state
explicitly that the author agrees to the dual licence.
