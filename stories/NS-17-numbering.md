# NS-17: Numbering rule (basic)

Status: Draft
Depends on: NS-1
Area: engine

## Why
Number files in order.

## Acceptance criteria
1. Given `a.txt`, `b.txt`, `c.txt`, suffix numbering, start 1, step 1, pad 2, then `a_01`... with separator `_` giving `a_01.txt`, `b_02.txt`, `c_03.txt`.
2. Given prefix mode, start 10, step 5, then `10_a.txt`, `15_b.txt`.
3. Given insert at position 2, then the number is inserted there.
4. Given pad 0 and 120 files, then numbers are not padded (`1`, `2`, ... `120`).
5. Given a number wider than the pad (`100` with pad 2), then it is not cut.
6. Given selected files only, then numbering starts at the start value for the first selected file and follows list order.
7. Given a negative step, then numbers count down; numbers below 0 are flagged as an invalid result.

## Out of scope
- Hex, letters, roman, per-folder break (NS-29).

## Decisions
- Order of files? **Decision:** the list's natural sort, because users expect numbers to follow what they see (po, 2026-10-18).

## Log
- 2026-10-18: created (po).
