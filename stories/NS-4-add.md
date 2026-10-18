# NS-4: Add rule

Status: Ready
Depends on: NS-1
Area: engine

## Why
Add text to the start, end or inside of a name.

## Acceptance criteria
1. Given `a.txt`, prefix `new_`, then `new_a.txt`.
2. Given `a.txt`, suffix `_v2`, then `a_v2.txt` (the suffix goes before the extension).
3. Given `abcdef.txt`, insert `-` at position 3, then `abc-def.txt` (position counts characters from the start of the name).
4. Given `ab.txt`, insert `X` at position 10 (past the end), then `abX.txt` (clamped to the end).
5. Given insert at position counted from the end, 2, on `abcdef.txt`, then `abcd-ef.txt` with text `-`.
6. Given the word-space option on `HelloWorldAgain.txt`, then `Hello World Again.txt` (a space is inserted before each upper-case letter that follows a lower-case letter or digit).
7. Given empty text, then the name is unchanged.

## Out of scope
- Anything not listed above.

## Decisions
- Position counting: from 0 or 1? **Decision:** the user types 1-based positions (position 1 = before the first character; 0 and negative are treated as 1), because that is what people count (po, 2026-10-18).

## Log
- 2026-10-18: created (po).
- 2026-10-18: set Ready, first round of the minimal usable slice (po).
