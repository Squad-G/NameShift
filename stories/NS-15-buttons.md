# NS-15: Rename, Revert and Reset buttons

Status: Ready
Depends on: NS-13, NS-14, NS-9
Area: app

## Why
Run the batch, undo it, or start over, from the window.

## Acceptance criteria
1. Given no change in any name, then Rename is disabled.
2. Given conflicts, then Rename is disabled and its hint says to fix the conflicts.
3. Given I click Rename with valid changes, then the batch runs, the list reloads showing the new names, and a message says `N files renamed`.
4. Given the batch fails and is rolled back, then a message shows which file failed and why, and the names are as before.
5. Given a batch was done, then Revert is enabled and one click restores the old names and reloads the list; with no entry it is disabled.
6. Given I click Reset, then all panels reset and unchecked (the folder and selection stay).
7. Given a batch of more than 500 files, then Rename asks for a one-line confirmation first; smaller batches do not.
8. Given the batch runs, then the window shows progress and buttons are disabled until it ends.
9. Given the result, then no dialog blocks on success; the message is in the status bar.

## Out of scope
- Anything not listed above.

## Decisions
- Confirm every rename? **Decision:** no confirmation under 500 files, because preview plus undo is the safety and extra clicks annoy (po, 2026-10-18).

## Log
- 2026-10-18: created (po).
- 2026-10-18: set Ready, first round of the minimal usable slice (po).
