# NS-28: Append folder name rule

Status: Draft
Depends on: NS-1
Area: engine

## Why
Add the name of the parent folder(s).

## Acceptance criteria
1. Given `C:\Photos\Paris\a.jpg`, prefix, levels 1, separator `_`, then `Paris_a.jpg`.
2. Given levels 2 suffix with separator `-`, then `a-Photos-Paris.jpg` (outer folder first when reading left to right).
3. Given levels greater than the depth, then the available ones are used.
4. Given a file at a drive root, then no folder name is added.
5. Given the path `C:\a.jpg` or a deeper path with levels larger than the depth, then the drive name (`C:`) is never used as a folder name.

## Out of scope
- Anything not listed above.

## Decisions
- None needed.

## Log
- 2026-10-18: created (po).
