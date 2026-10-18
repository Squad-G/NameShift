# NS-14: Choose which files to rename

Status: In Progress
Depends on: NS-51, NS-13
Area: app

## Why
Rename only the files I pick, not always the whole folder.

## Acceptance criteria
1. Given no selection, then every listed file is included in the batch.
2. Given I select some rows (click, Shift, Ctrl, Ctrl+A), then only those are renamed and the others show no New Name.
3. Given an unselected file would collide with a new name, then the collision is still detected (`TargetExists`).
4. Given I select rows in any order (click order differs from list order), then the pipeline receives the selected items in list order with indexes 0..n-1 and an item count of n (so a rule that depends on position, such as numbering NS-17, follows the list, not the clicks).
5. Given the selection changes, then the preview updates and a `N of M files` count shows.

## Out of scope
- Anything not listed above.

## Decisions
- Checkboxes or selection? **Decision:** normal row selection, because it is what Explorer users know (po, 2026-10-18).

## Log
- 2026-10-18: created (po).
- 2026-10-18: set Ready, first round of the minimal usable slice (po).
- 2026-10-18: In Progress, branch feature/NS-14-selection (builder).
