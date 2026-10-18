# NS-17: Numbering rule (basic)

Status: Done
Depends on: NS-1
Area: engine

## Why
Number files in order.

## Acceptance criteria
1. Given `a.txt`, `b.txt`, `c.txt`, suffix numbering, start 1, step 1, pad 2, separator `_`, then `a_01.txt`, `b_02.txt`, `c_03.txt`.
2. Given `a.txt`, `b.txt`, prefix mode, start 10, step 5, separator `_`, then `10_a.txt`, `15_b.txt`.
3. Given `abcdef.txt`, insert mode at position 2, start 1, pad 0, no separator, then `a1bcdef.txt` (1-based as in NS-4: one character stays in front).
4. Given pad 0 and 120 files, then numbers are not padded (`1`, `2`, ... `120`).
5. Given a number wider than the pad (`100` with pad 2), then it is not cut.
6. Given selected files only, then numbering starts at the start value for the first selected file and follows list order.
7. Given a negative step, then numbers count down; a number below 0 is not produced: that item gets an error status (NS-1, "number below zero") and keeps its name.
8. Given no separator is set, then the default is none: `a.txt`, suffix mode, start 1 gives `a1.txt`.

## Out of scope
- Hex, letters, roman, per-folder break (NS-29).

## Decisions
- Order of files? **Decision:** the list's natural sort, because users expect numbers to follow what they see (po, 2026-10-18).

- Default separator in the engine? **Decision:** none, so the rule adds only what is asked; the panel's default is `_` (NS-53) (tech-lead, 2026-10-18).

- Where does the separator go in insert mode? **Decision:** after the number (`abc1_def`), as in prefix mode (tech-lead default, 2026-10-18; po may overrule).
- Insert mode without a valid position? **Decision:** the rule does nothing, as Add's insert part (tech-lead default, 2026-10-18; po may overrule).
- Largest pad? **Decision:** 255 digits (a Windows name's length); the panel validates its own field (tech-lead default, 2026-10-18; po may overrule).
- Separator in insert mode at or past the end of the name? **Decision:** (tech-lead, 2026-10-18, from the NS-17 review) in insert mode the separator is only added when text follows the number.

## Log
- 2026-10-18: created (po).
- 2026-10-18: set Ready and added to the first round after review: numbering is the most common bulk-rename need, so the minimal usable slice includes it (with its panel, NS-53) (po).
- 2026-10-18: In Progress (builder).
- 2026-10-18: In Review, PR #8 (builder).
- 2026-10-18: Done, PR #8 merged after review; folded the review's nits (pad 255 test, no separator at the end in insert mode) (tech-lead).
