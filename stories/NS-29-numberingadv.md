# NS-29: Numbering rule: bases, roman numerals, break per folder

Status: Draft
Depends on: NS-17
Area: engine

## Why
The rest of the numbering options.

## Acceptance criteria
1. Given base hex, start 10, pad 2, then `0A`, `0B` (upper-case digits by default).
2. Given base letters, start 1, then `a`, `b`, ... `z`, `aa`, `ab`.
3. Given roman numerals, start 4, then `IV`, `V`; values above 3999 or below 1 are flagged invalid.
4. Given break per folder with files in two folders, then numbering restarts at the start value in each folder.
5. Given start values in each base are typed in that base (hex `FF`), then they are parsed or the field is marked invalid.

## Out of scope
- Anything not listed above.

## Decisions
- None needed.

## Log
- 2026-10-18: created (po).
