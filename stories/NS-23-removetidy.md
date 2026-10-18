# NS-23: Remove: trim, leading dots, double spaces

Status: Draft
Depends on: NS-1
Area: engine

## Why
Tidy messy names.

## Acceptance criteria
1. Given `  a b .txt` (leading and trailing spaces in the base name), trim, then `a b.txt`.
2. Given `..a.txt`, remove leading dots, then `a.txt`; `.gitignore` becomes `gitignore` only when this option is on.
3. Given `a   b.txt`, remove double spaces, then `a b.txt` (runs collapse to one space).
4. Given nothing to tidy, then the name is unchanged.

## Out of scope
- Anything not listed above.

## Decisions
- None needed.

## Log
- 2026-10-18: created (po).
