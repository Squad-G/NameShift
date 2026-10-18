# NS-15: Rename, Revert and Reset buttons

Status: Ready
Depends on: NS-13, NS-52, NS-14, NS-9
Area: app

## Why
Run the batch, undo it, or start over, from the window.

## Acceptance criteria
1. Given no change in any name, then Rename is disabled.
2. Given conflicts (NS-52), then Rename is disabled and its hint says to fix the conflicts. This is the only place that rule is written.
3. Given I click Rename with valid changes, then the batch runs, the list reloads showing the new names, and a message says `N files renamed`.
4. Given the batch fails and is rolled back, then a message shows which file failed and why, and the names are as before.
5. Given a batch was done, then Revert is enabled and one click restores the old names and reloads the list; with no entry, or right after a Revert (NS-9), it is disabled, except after a partial Revert (NS-9:8): it then stays available for the files that could not be reverted.
6. Given I click Reset, then all panels reset and unchecked (the folder and selection stay).
7. Given a batch of 501 files or more, then Rename asks for a one-line confirmation first; a batch of 500 or fewer does not.
8. Given the batch runs, then the window shows progress and buttons are disabled until it ends.
9. Given the result, then no dialog blocks on success; the message is in the status bar.

## Out of scope
- Anything not listed above.

## Decisions
- Confirm every rename? **Decision:** no confirmation up to 500 files, because preview plus undo is the safety and extra clicks annoy (po, 2026-10-18).

## Log
- 2026-10-18: created (po).
- 2026-10-18: set Ready, first round of the minimal usable slice (po).
