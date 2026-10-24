# NS-19: Name rule: keep, remove, fixed, reverse

Status: Ready
Depends on: NS-1
Area: engine

## Why
Replace the whole base name or reshape it.

## Acceptance criteria
`NameRule(NameMode mode, string value = "")`, stage Name (2), modes Keep, Fixed, Remove, Reverse. It changes the base
name only; the extension is never touched.
1. Given mode fixed `holiday` on `IMG_1.jpg`, then `holiday.jpg`; on a folder `Old` then `holiday`.
2. Given mode fixed with an empty value, then the name is unchanged and `Enabled` is false (no accidental empty names).
3. Given mode remove on `a.txt`, then the rule returns an empty base name with extension `txt`, and the existing
   invalid-name check flags `EmptyName` (pipeline test with the checker). Given a later Add rule with prefix `x`,
   then the result is `x.txt` and not flagged.
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
- Reverse unit? **Decision:** text elements (`StringInfo`), so accents and emoji survive (po, 2026-10-23).

## Log
- 2026-10-18: created (po).
- 2026-10-23: criteria made testable, empty-fixed decided, set Ready (po).
