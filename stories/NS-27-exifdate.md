# NS-27: Auto date rule: photo (EXIF) date

Status: Draft
Depends on: NS-26
Area: engine

## Why
Name photos by the date they were taken.

## Acceptance criteria
1. Given a JPEG with EXIF DateTimeOriginal 2023-07-01 10:00:00, then the Auto date rule can use it as the source.
2. Given a file without EXIF, then the item falls back to the modified date and the preview marks the fallback; an option makes it an error instead.
3. Given a damaged or non-image file, then no crash occurs and the fallback applies.
4. Reading EXIF uses only built-in .NET (System.Drawing metadata), no new dependency.

## Out of scope
- RAW formats, video metadata.

## Decisions
- Fallback or error? **Decision:** fall back to modified date and mark it, because a stopped batch for one screenshot is annoying (po, 2026-10-18).

## Log
- 2026-10-18: created (po).
