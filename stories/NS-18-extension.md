# NS-18: Extension rule

Status: Draft
Depends on: NS-1
Area: engine

## Why
Change or remove file extensions safely.

## Acceptance criteria
1. Given `a.JPG`, mode lower, then `a.jpg`; mode upper on `a.jpg` gives `a.JPG`; mode same changes nothing.
2. Given mode fixed `png` on `a.jpg`, then `a.png`; a leading dot in the typed value is ignored (`.png` = `png`).
3. Given mode extra `bak` on `a.txt`, then `a.txt.bak`.
4. Given mode remove on `a.txt`, then `a`; on `Makefile` nothing changes.
5. Given fixed on a file with no extension, then the extension is added.
6. Given the new extension is empty and mode is fixed, then it is treated as remove.

## Out of scope
- Anything not listed above.

## Decisions
- Does it ever rename the real file type? **Decision:** it only renames; the file content is not converted, and the UI says so (po, 2026-10-18).

## Log
- 2026-10-18: created (po).
