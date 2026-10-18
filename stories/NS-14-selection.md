# NS-14: Choose which files to rename

Status: Ready
Depends on: NS-10, NS-13
Area: app

## Why
Rename only the files I pick, not always the whole folder.

## Acceptance criteria
1. Given no selection, then every listed file is included in the batch.
2. Given I select some rows (click, Shift, Ctrl, Ctrl+A), then only those are renamed and the others show no New Name.
3. Given an unselected file would collide with a new name, then the collision is still detected (`TargetExists`).
4. Given numbering will exist later, then selected files are numbered in list order, not click order.
5. Given the selection changes, then the preview updates and a `N of M files` count shows.

## Out of scope
- Anything not listed above.

## Decisions
- Checkboxes or selection? **Decision:** normal row selection, because it is what Explorer users know (po, 2026-10-18).

## Log
- 2026-10-18: created (po).
- 2026-10-18: set Ready, first round of the minimal usable slice (po).
