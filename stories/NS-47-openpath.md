# NS-47: Open a folder from the command line or by drag and drop

Status: Draft
Depends on: NS-10
Area: app

## Why
Start NameShift on a folder quickly.

## Acceptance criteria
1. Given `NameShift.exe "D:\Pics"`, then the app opens that folder.
2. Given I drop a folder on the window, then it opens; dropping a file opens its folder and selects the file.
3. Given a path that does not exist, then the app opens normally with a message.
4. Given paths with spaces or a trailing backslash, then they are handled.

## Out of scope
- Anything not listed above.

## Decisions
- Relation to the command-line stories (NS-38 to NS-43)? **Decision:** this story is only the window: `NameShift.exe <folder>` opens the window on a folder. Renaming from a console is the `apply`/`undo` verbs of NS-38 onward; a bare folder argument with no verb opens the window, so there is no overlap (po, 2026-10-18).
- Multiple dropped items? **Decision:** only the first folder, to stay simple (po, 2026-10-18).

## Log
- 2026-10-18: created (po).
