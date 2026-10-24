# NS-19: Name rule: keep, remove, fixed, reverse

Status: In Progress
Depends on: NS-1
Area: engine

## Why
Replace the whole base name or reshape it.

## Acceptance criteria
`NameRule(NameMode mode, string value = "")`, stage Name (2), modes Keep, Fixed, Remove, Reverse. It changes the base
name only; the extension is never touched.
1. Given mode fixed `holiday` on `IMG_1.jpg`, then `holiday.jpg`; on a folder `Old` then `holiday`.
2. Given mode fixed with an empty value, then the name is unchanged and `Enabled` is false (no accidental empty names).
3. Given mode remove on the file `a.txt`, then the rule returns an empty base name with extension `txt`; the full
   name is `.txt`, which is a valid name (NS-5, like `.gitignore`) and is not flagged. Given mode remove on a folder
   `Old` (no extension), then the name is empty and the existing invalid-name check flags `EmptyName` (pipeline test
   with the checker). Given a later Add rule with prefix `x` on the file, then the result is `x.txt`.
4. Given mode reverse on `abc123.txt`, then `321cba.txt`; on `a.tar.gz` then `rat.a.gz` (the base is `a.tar`).
5. Given mode keep, then the name is unchanged and `Enabled` is false.
6. Given reverse on `e` + combining acute (U+0301) + `x.txt`, then the accent stays on its letter (`x` then `e` + U+0301);
   given a surrogate pair (an emoji) in the base, then the pair stays whole and in order.
7. Given reverse on a name that is one character or a palindrome (`aba.txt`), then it is unchanged.
8. Given a typed value with Windows-invalid characters, then it is applied as typed and the invalid-name check
   flags it.
9. Given fixed `x` over a list `a.txt`, `b.txt`, then both become `x.txt` and the duplicate check flags them
   (the rule does not number them: that is Numbering).
10. Given an unknown mode, then the constructor throws `ArgumentOutOfRangeException`.

## Out of scope
- The panel (NS-55).
- Numbering duplicates (NS-17 and NS-29 do that).

## Decisions
- Is `keep` needed? **Decision:** yes as the default, so the panel has a neutral state (po, 2026-10-18).
- Fixed with empty text? **Decision:** does nothing; Remove is the way to empty a name, so an empty box never wipes
  names by accident (po, 2026-10-23).
- Reverse unit? **Decision:** text elements (`StringInfo`), so accents and emoji survive (po, 2026-10-23). Only the
  cases in criterion 6 (combining accent, surrogate pair) are guaranteed: the app runs on .NET Framework 4.8 and the
  tests on net10.0, where grapheme segmentation differs for joined emoji and flags.
- Empty base name on a file? **Decision:** `.txt` is valid (NS-5 decided it on purpose); only a folder with an empty
  result is flagged `EmptyName` (po, 2026-10-23).
- Fixed empty vs NS-18? **Decision:** both rules treat an empty Fixed value as "does nothing"; Remove is the way to
  empty a name or an extension (po, 2026-10-23).

## Log
- 2026-10-18: created (po).
- 2026-10-23: criteria made testable, empty-fixed decided, set Ready (po).
- 2026-10-23: In Progress (builder).
