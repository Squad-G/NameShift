# NS-6: Duplicate target detection

Status: Done
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
- Names and folder paths are compared with `StringComparer.OrdinalIgnoreCase`. It is close to, but not exactly, the volume's NTFS `$UpCase` table (rare characters may differ, and .NET Framework and .NET may differ on them); a rare miss is caught by NS-8, which checks each target on disk right before renaming and never replaces. No Unicode normalization here (NS-48 owns it) (tech-lead, 2026-10-18).
- The existing names are given to the check's constructor by the app, one list per folder (the folder's real listing, hidden and system entries included). Folder paths are not normalized: the app uses the same string as the items' `FolderPath`. A batch folder without a list is a programming error (`InvalidOperationException`) (tech-lead, 2026-10-18).
- Only changed items are flagged; unchanged and error items keep and occupy their name. A changed item landing on a batch item's final name (unchanged, error, or another changed item's target) is `DuplicateTarget`; `TargetExists` is only for entries outside the batch. Files the user did not select (NS-14) are not in the batch, so they count as existing (matches NS-52:5) (tech-lead default, 2026-10-18; po may overrule).
- Both codes can apply to one item (`DuplicateTarget` then `TargetExists`); details are null (the UI states the reason) (tech-lead, 2026-10-18).
## Log
- 2026-10-18: created (po).
- 2026-10-18: set Ready, first round of the minimal usable slice (po).
- 2026-10-18: In Progress, branch feature/NS-6-duplicates (builder).
- 2026-10-18: In Review, PR #10 (builder).
- 2026-10-18: Done, PR #10 merged; review nits folded in (separate target parameter in Check_NameOfAnEntryOutsideTheBatch_FlagsTargetExists, constructor parameter renamed existingNames) (tech-lead).
