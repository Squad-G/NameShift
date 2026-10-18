# NS-20: Remove: first n, last n, from-to

Status: Draft
Depends on: NS-1
Area: engine

## Why
Cut characters by position.

## Acceptance criteria
1. Given `IMG_0001.jpg`, remove first 4, then `0001.jpg`.
2. Given `file_copy.txt`, remove last 5, then `file.txt`.
3. Given `abcdef.txt`, remove from 2 to 4 (1-based, inclusive), then `aef.txt`.
4. Given n larger than the name length, then the base name becomes empty (flagged `EmptyName`).
5. Given from > to or from < 1, then the part is ignored.
6. Given n = 0, then nothing is removed.

## Out of scope
- Anything not listed above.

## Decisions
- Positions are 1-based and inclusive, as in Add (po, 2026-10-18).

## Log
- 2026-10-18: created (po).
