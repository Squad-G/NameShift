# NS-16: Replace rule: keep the case pattern of the found text

Status: Done
Depends on: NS-2, NS-12
Area: engine, app

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
   Capitalising reuses `CaseRule.Capitalise`, which skips leading characters that are neither letters nor digits: find
   `dump` with `_bob`, `Dump.txt` becomes `_Bob.txt`; with `1st`, `Dump.txt` becomes `1st.txt` (a leading digit
   counts as the first character, so nothing is upper-cased).
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
    `NameShift.Ui.Strings`, text "Match the &case of the found text", Alt+C), off by default, cleared by the panel's
    reset (`IsDefault` false while it is on), and `BuildRule` passes it to the rule. A model test covers the mapping,
    default and reset. The checkbox sits after Match case (by hand, listed in the PR).
12. The mnemonic test is changed from "unique across the window" to David's rule: within one rule panel, the title and
    all its labels have distinct Alt letters; the panel titles are distinct among themselves (today R, C, A, N). Given
    the same letter in two different panels (Replace's Alt+C on the new checkbox and the Case title), then the test
    passes; given a repeat inside one panel, or two panels with the same title letter, then it fails (a test of the
    check itself with sample labels). Later panel stories follow this rule.

## Out of scope
- Presets (NS-36) and the command-line option `--replace-keep-case` (NS-40): until they exist the panel model keeps
  the setting as a plain public property and nothing more; the story that builds them adds the field (same policy
  for every panel story).
- Cultural casing (all casing is invariant, as in the Case rule).

## Decisions
- Default? **Decision:** off, see the Replace rule (po, 2026-10-18).
- What counts as the pattern? **Decision:** all letters upper (and at least two letters) = upper; first letter upper,
  rest lower (or one upper letter) = capitalised; all lower = as typed; anything else = as typed. Reason: the three
  patterns people mean, nothing clever for odd cases (po, 2026-10-23).
- Presets and CLI in criterion 5 of the draft? **Decision:** removed from this story, since neither exists yet; each
  of those stories picks the setting up (po, 2026-10-23).
- The seam: `ReplacementFor(found)` already exists in ReplaceRule for this (po, 2026-10-23).
- Alt-key letters? **Decision:** unique within a panel only; repeats across panels are allowed (Windows cycles focus
  between them). Panel titles stay distinct among themselves. This replaces the window-wide rule of NS-12 (David,
  2026-10-23). The checkbox gets C: Replace uses R F W M X, so C is free inside it.

## Log
- 2026-10-18: created (po).
- 2026-10-23: criteria made testable, presets and CLI moved out, set Ready (po).
- 2026-10-23: back to Draft, blocked on the Alt-letter question; leading non-letter case added (po, from the PR #24 review).
- 2026-10-23: David chose per-panel mnemonics; letter C and the test change (criterion 12) added; set Ready (po).
- 2026-10-23: In Progress (builder).
- 2026-10-23: In Review (builder), PR #27.
- 2026-10-24: Done (tech-lead), PR #27 merged into dev. Hand checks still to be done by David.
