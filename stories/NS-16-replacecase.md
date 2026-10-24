# NS-16: Replace rule: keep the case pattern of the found text

Status: Ready
Depends on: NS-2, NS-12
Area: engine

## Why
Optionally make the replacement follow the capitalisation of what it replaces.

## Acceptance criteria
The option is `ReplaceRule`'s new last optional constructor parameter `keepCase` (default false). The pattern is read
from the letters of each found text (non-letters are skipped); every match is judged on its own.
1. Given the option off (default), then behaviour is unchanged from the Replace rule (the existing NS-2 tests pass
   unchanged).
2. Given the option on, find `dump` with `bob`, match case off, then `Dump.txt` becomes `Bob.txt`, `DUMP.txt` becomes
   `BOB.txt`, `dump.txt` becomes `bob.txt`.
3. Given the option on and a found text with mixed case (`dUmP`, `DuMp`), then the replacement is used as typed.
4. Given the option on, find `dump` with `bob stack`, then `Dump.txt` becomes `Bob stack.txt` (capitalised: only the
   first letter of the replacement changes) and `DUMP.txt` becomes `BOB STACK.txt` (upper: the whole replacement).
5. Given the option on and a one-letter found text, find `a` with `xyz`, then `A.txt` becomes `Xyz.txt` (a single
   upper-case letter counts as capitalised, not as all upper-case) and `a.txt` becomes `xyz.txt`.
6. Given the option on and a found text with no letters (find `1` with `one`), or a replacement with no letters
   (`x` found as `X` with `42`), then the replacement is used as typed.
7. Given the option on, a replacement typed with its own capitals (`BoB`) and a found text in lower case, then it stays
   `BoB` (a lower-case match never lowers the replacement).
8. Given an empty With and the option on, then the found text is removed exactly as without the option.
9. Given the option on with match case on, then it applies the same way (the found text then equals Find, so the
   pattern is Find's).
10. Given the option on with Include extension on, then `DUMP.TXT`, find `txt` with `log`, becomes `DUMP.LOG`.
11. Given the Replace panel (NS-12), then it has a checkbox "Match the case of the found text" (label from
    `NameShift.Ui.Strings`, unique mnemonic checked by the existing test), off by default, cleared by the panel's
    reset (`IsDefault` false while it is on), and `BuildRule` passes it to the rule. A model test covers the mapping,
    default and reset. The checkbox sits after Match case (by hand, listed in the PR).

## Out of scope
- Presets (NS-36) and the command-line option `--replace-keep-case` (NS-40): those stories add their own hook for it
  when built; nothing here for them.
- Cultural casing (all casing is invariant, as in the Case rule).

## Decisions
- Default? **Decision:** off, see the Replace rule (po, 2026-10-18).
- What counts as the pattern? **Decision:** all letters upper (and at least two letters) = upper; first letter upper,
  rest lower (or one upper letter) = capitalised; all lower = as typed; anything else = as typed. Reason: the three
  patterns people mean, nothing clever for odd cases (po, 2026-10-23).
- Presets and CLI in criterion 5 of the draft? **Decision:** removed from this story, since neither exists yet; each
  of those stories picks the setting up (po, 2026-10-23).
- The seam: `ReplacementFor(found)` already exists in ReplaceRule for this (po, 2026-10-23).

## Log
- 2026-10-18: created (po).
- 2026-10-23: criteria made testable, presets and CLI moved out, set Ready (po).
