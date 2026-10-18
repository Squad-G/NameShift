# NS-32: Filters: mask, regex, kinds and length

Status: Draft
Depends on: NS-1
Area: engine

## Why
Decide which files take part.

## Acceptance criteria
1. Given mask `*.jpg;*.png`, then only matching files are included (case-insensitive, `;` separated, `*` and `?` wildcards).
2. Given regex filter `^IMG_\d+`, then only matching base names are included.
3. Given files only / folders only / both, then only those kinds are included.
4. Given hidden off (default), then hidden and system files are excluded; on includes them.
5. Given min length 5 and max length 8 on the base name, then names outside that range are excluded.
6. Given an excluded file, then it is not renamed but still counts for conflict detection.
7. Given an invalid regex, then the filter reports an error and excludes nothing.

## Out of scope
- Anything not listed above.

## Decisions
- Match against name or full name? **Decision:** the original name with extension for masks, and the base name for regex and length, since masks are usually about extensions (po, 2026-10-18).

## Log
- 2026-10-18: created (po).
