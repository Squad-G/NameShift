# NS-25: Move or copy parts of the name

Status: Draft
Depends on: NS-1
Area: engine

## Why
Reorder a name by moving or copying a few characters.

## Acceptance criteria
1. Given `2024report.txt`, move first 4 chars to the end with separator `_`, then `report_2024.txt`.
2. Given `report_2024.txt`, copy last 4 chars to the start with separator `-`, then `2024-report_2024.txt`.
3. Given move to position 3 of `abcdef.txt` with the first 2 chars, then `cdabef.txt`.
4. Given n larger than the name, then the whole base name is used.
5. Given n = 0, then nothing happens.

## Out of scope
- Anything not listed above.

## Decisions
- None needed.

## Log
- 2026-10-18: created (po).
