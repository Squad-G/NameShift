# NS-26: Auto date rule (file dates)

Status: Draft
Depends on: NS-1
Area: engine

## Why
Add the file's created or modified date to its name.

## Acceptance criteria
1. Given modified date 2024-03-09 14:05:07, format `yyyy-MM-dd`, prefix with separator `_`, then `2024-03-09_a.txt`.
2. Given format `yyyyMMdd_HHmmss`, suffix, then `a_20240309_140507.txt`.
3. Given created instead of modified, then the creation date is used.
4. Given a custom format string with invalid characters for file names (`:`), then they are flagged by the invalid-names check, not changed.
5. Given the culture is French, then month names in a `MMMM` format use invariant (English) text so batches are reproducible.
6. Dates are taken in local time.

## Out of scope
- EXIF (NS-27).

## Decisions
- Date source time zone? **Decision:** local time, which matches what Explorer shows (po, 2026-10-18).

## Log
- 2026-10-18: created (po).
