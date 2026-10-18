# NS-6: Duplicate target detection

Status: Ready
Depends on: NS-1
Area: engine

## Why
Never overwrite or lose a file because two files get the same name.

## Acceptance criteria
1. Given `a.txt` and `b.txt` both renamed to `x.txt`, then both items are flagged `DuplicateTarget`.
2. Given `a.txt` renamed to `x.txt` and `b.txt` renamed to `X.TXT` in one folder, then both are flagged as duplicates (comparison is case-insensitive, as on Windows).
3. Given a new name equal to another entry that exists in the folder and is not part of the batch, then the item is flagged `TargetExists`. The set of existing names comes from the real directory listing, including hidden and system files and entries that the window's list does not show (it never comes from the visible list).
4. Given a new name equal to a file that is in the batch but itself being renamed away (`a`->`b`, `b`->`c`), then neither is flagged (a chain is valid).
5. Given a swap `a`->`b` and `b`->`a`, then neither is flagged (the executor handles it, NS-8).
6. Given a case-only change `photo.jpg` -> `Photo.jpg`, then it is not flagged as existing.
7. Given the same new name in two different folders, then no conflict.

## Out of scope
- Anything not listed above.

## Decisions
- Auto-add a suffix on conflict? **Decision:** no, block and show; the user can add numbering, because guessing names is unsafe (po, 2026-10-18).

## Log
- 2026-10-18: created (po).
- 2026-10-18: set Ready, first round of the minimal usable slice (po).
