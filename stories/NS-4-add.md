# NS-4: Add rule

Status: In Progress
Depends on: NS-1
Area: engine

## Why
Add text to the start, end or inside of a name.

## Acceptance criteria
1. Given `a.txt`, prefix `new_`, then `new_a.txt`.
2. Given `a.txt`, suffix `_v2`, then `a_v2.txt` (the suffix goes before the extension).
3. Given `abcdef.txt`, insert `-` at position 4, then `abc-def.txt` (1-based: position 4 is the place before the 4th character, so three characters stay in front).
4. Given `ab.txt`, insert `X` at position 10 (past the end), then `abX.txt` (clamped to the end).
5. Given insert counted from the end with value 2 on `abcdef.txt` and text `-`, then `abcd-ef.txt` (the value is how many characters stay after the inserted text).
6. Given a position that is empty, 0, negative or not a number, then the insert part is ignored and the name is unchanged for that part.
7. Given the word-space option on `HelloWorldAgain.txt`, then `Hello World Again.txt` (a space is inserted before each upper-case letter that follows a lower-case letter or digit).
8. Given empty text, then the name is unchanged.

## Out of scope
- Anything not listed above.

## Decisions
- Position counting: from 0 or 1? **Decision:** the user types 1-based positions (position 1 = before the first character), because that is what people count; a position below 1 or not a number means the part is ignored, the same as every other positional rule (Remove, Move/Copy), so the behaviour is one everywhere (po, 2026-10-18).
- Order inside Add: word space, then insert, then prefix and suffix, so positions count the name as it reaches Add and typed texts are never split by word space (tech-lead default, 2026-10-18; po may overrule).
- From-the-end value: the characters left after the inserted text; values below 1 are ignored (the suffix covers the very end) (tech-lead default, 2026-10-18; po may overrule).

## Log
- 2026-10-18: created (po).
- 2026-10-18: set Ready, first round of the minimal usable slice (po).
- 2026-10-18: In Progress (builder).
