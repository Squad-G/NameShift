# NS-57: Rename stays off until something changes

Status: Ready
Depends on: NS-15
Area: app

## Why
After a successful Rename the rules stay as they are, and the reloaded list is previewed again. With a rule such as
Add (suffix `_x`), a second click would rename the same files again (`a_x_x.txt`). The Rename button stays off until the
user changes something, so a double click or a habit click cannot rename twice by accident.

## Background (current code)
`MainActions.CanRename` is the only place the rule is written: not busy, list loaded, preview current and not failed,
at least one changed name, no conflict, no rule error. `RunAsync` reloads the folder after the action and the preview
recomputes on that reload, so today Rename is on again right after it ran.

## Acceptance criteria
In `MainActions` (tests in `NameShift.Ui.Tests`, with the fake renamer and a fake file source):
1. Given a Rename that renamed at least one file (`Execution.Renamed` not empty), when it completes and the folder has
   reloaded, then `CanRename` is false, even though the preview shows changed names, and `RenameHint` is "" (no
   conflict to explain). The rules, their values and their enabled state are unchanged.
2. Given a Rename that was started but renamed nothing (every item failed, or the batch was not started because the
   journal could not be written), then `CanRename` is true again as before: nothing changed on disk, so the user may
   fix the problem and retry.
3. Given `CanRename` is false because of criterion 1, then each of these makes it true again (when the other
   conditions hold), and only these:
   - a rule edit (any `RulePanelHostModel.RulesChanged`: a value, enable or disable of a rule, or Reset of a rule or
     all rules),
   - a change of selection (`FileListModel.SelectionChanged`),
   - the shown folder changing (navigating to another folder, or to the same one by a typed path that reloads it
     through `FileListModel.LoadAsync` outside an action).
4. Given the action's own reload (inside `RunAsync`, while `IsBusy`), then events from it (`Changed`,
   `SelectionChanged`, the preview's `Changed`) do not clear the lock: a Rename never unlocks itself.
5. Given Rename clicked while `CanRename` is false because of this lock, then `RenameAsync` returns null and nothing
   runs (it already does for any false `CanRename`).
6. Given a Revert that ran (even with skipped items), then the lock is cleared: the names changed back, so the
   preview matches the old one and Rename is allowed again if the usual conditions hold. Given a Revert that was not
   allowed (`CanRevert` false), the lock is untouched.
7. Given two Renames in a row with a rule edit in between, then the second runs normally and locks again.
8. Given the lock, then `CanReset`, `CanRevert` and `IsBusy` are not affected, and `Changed` is raised when the lock
   is set or cleared so the buttons update.

In the window (by hand, listed in the PR):
9. Given a Rename that worked, then the Rename button is greyed, the status bar still shows the result of the
   rename, and the Rename button's accessible description or a tooltip says why (new string in `Strings.resx`:
   "Change a rule or the selection to rename again."). Editing any rule box re-enables it after the usual preview delay.

## Out of scope
- Clearing or auto-resetting the rules after a Rename (David wants them kept).
- Disabling Rename when a file list shows names that already match the rules (no change means `ChangedCount` 0 and
  Rename is already off).
- Remembering the lock across a restart or per folder.

## Decisions
- Lock until what? **Decision:** a rule change, a selection change or a different folder shown; these are the cases
  where the user has looked at a new preview (David, 2026-10-23).
- Only after a successful Rename? **Decision:** only when at least one file was renamed; a run that renamed nothing
  leaves the disk unchanged, so a retry is safe and the user needs it (po, 2026-10-23).
- After Revert? **Decision:** the lock is cleared. Revert puts the old names back, so the preview shows the same
  change as before the Rename and running it again is what the user asks for; the rules did not move, and the
  journal protects the new run (Revert is off or points to the new batch). Staying locked would need a rule edit for no
  safety gain (po, 2026-10-23).
- Where does the lock live? **Decision:** in `MainActions` beside `CanRename`, the one place the rule is written; no
  second source of truth in the buttons (po, 2026-10-23).
- Does an own-reload selection reset count as a change? **Decision:** no (criterion 4), otherwise the lock would
  clear itself at once (po, 2026-10-23).

## Log
- 2026-10-23: created and set Ready, after David's decision on the double-rename risk (po).
