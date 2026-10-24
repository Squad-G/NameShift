# NS-57: Rename stays off until something changes

Status: In Review
Depends on: NS-15
Area: app

## Why
After a successful Rename the rules stay as they are, and the reloaded list is previewed again. With a rule such as
Add (suffix `_x`), a second click would rename the same files again (`a_x_x.txt`). The Rename button stays off until the
user changes something, so a double click or a habit click cannot rename twice by accident.

## Background (current code)
`MainActions.CanRename` is the only place the rule is written: not busy, list loaded, preview current and not failed,
at least one changed name, no conflict, no rule error. `RunAsync` reloads the folder after the action and the preview
recomputes on that reload, so today Rename is on again right after it ran. `ActionButtons.Render` shows
`RenameHint` in a label under the buttons and copies it to the Rename button's accessible description; a disabled
WinForms button shows no tooltip, so the reason goes through `RenameHint`. While an action runs, `MainForm` disables
the tree, the path bar and the whole work area, so the user cannot navigate or edit rules meanwhile.

## Acceptance criteria
In `MainActions` (tests in `NameShift.Ui.Tests`, with the fake renamer and a fake file source):
1. Given a Rename that renamed at least one file (`Execution.Renamed` not empty), when it completes and the folder has
   reloaded, then `CanRename` is false, even though the preview shows changed names. The rules, their values and
   their enabled state are unchanged.
2. Given a Rename that was started but renamed nothing (every item failed, or the batch was not started because the
   journal could not be written), then the lock is not set and `CanRename` is true again as before: nothing changed on
   disk, so the user may fix the problem and retry.
3. Given the unexpected-failure path (`ActionUnexpected`: the renamer threw, disk state unknown), then the lock is set:
   safe over clever, a retry could rename twice.
4. Given `RenameHint` while locked (and no conflict or error), then it is the new string "Change a rule or the selection
   to rename again." (`Strings.resx`); a conflict or error hint still wins when there is one. It is "" while busy, as
   now. `ActionButtons` already shows it in the hint label and the button's accessible description.
5. Given the lock is set, then it is cleared by each of these, and by nothing else except criterion 7:
   - a rule edit (any `RulePanelHostModel.RulesChanged`: a value, enable or disable of a rule, Reset of a rule or all),
   - a change of selection (`FileListModel.SelectionChanged`),
   - the shown folder changing (a `FileListModel.LoadAsync` of any folder outside an action, navigating to another
     folder or to the same one through the path bar).
6. Given the action's own reload (inside `RunAsync`, while `IsBusy`), then events from it (`Changed`,
   `SelectionChanged`, the preview's `Changed`) do not clear the lock: a Rename never unlocks itself. The lock is set
   only if, at the end of the action, the shown folder is still the folder the Rename ran in; otherwise there is no
   lock (the new folder shows a different preview).
7. Given a Revert that ran (even with skipped items), then the lock is cleared: the names changed back, so the preview
   matches the old one and Rename is allowed again if the usual conditions hold. Given a Revert that was not allowed
   (`CanRevert` false), the lock is untouched.
8. Given Rename clicked while locked, then `RenameAsync` returns null and nothing runs (as for any false `CanRename`).
9. Given two Renames in a row with a rule edit in between, then the second runs normally and locks again.
10. Given the lock, then `CanReset`, `CanRevert` and `IsBusy` are not affected, and `Changed` is raised when the lock
    is set or cleared so the buttons update.

In the window (by hand, listed in the PR):
11. Given a Rename that worked, then the Rename button is greyed, the status bar still shows the result of the
    rename, the hint label shows the new text, and editing any rule box re-enables the button after the usual preview
    delay and hides the hint.

## Out of scope
- Clearing or auto-resetting the rules after a Rename (David wants them kept).
- Remembering the lock across a restart or per folder.

## Decisions
- Build order? **Decision:** after NS-56: both change `MainActions.RunAsync` and its tests (po, 2026-10-23).
- Lock until what? **Decision:** a rule change, a selection change or a different folder shown; these are the cases
  where the user has looked at a new preview (David, 2026-10-23).
- Only after a successful Rename? **Decision:** only when at least one file was renamed; a run that renamed nothing
  leaves the disk unchanged, so a retry is safe and the user needs it (po, 2026-10-23).
- After an unexpected failure? **Decision:** locked, the disk state is unknown and Revert is off (po, 2026-10-23).
- After Revert? **Decision:** the lock is cleared. Revert puts the old names back, so the preview shows the same
  change as before the Rename and running it again is what the user asks for; the rules did not move, and the
  journal protects the new run. Staying locked would need a rule edit for no safety gain (po, 2026-10-23).
- How is the reason shown? **Decision:** through `RenameHint`, the existing label and accessible description; no
  tooltip, since WinForms shows none on a disabled button (po, 2026-10-23, from the PR #24 review).
- Navigation during an action? **Decision:** the window blocks it while busy; in the model the lock is only set when
  the shown folder is still the action's folder (criterion 6).
- Where does the lock live? **Decision:** in `MainActions` beside `CanRename`, the one place the rule is written; no
  second source of truth in the buttons (po, 2026-10-23).
- Does an own-reload selection reset count as a change? **Decision:** no (criterion 6), otherwise the lock would
  clear itself at once (po, 2026-10-23).

## Log
- 2026-10-23: created and set Ready, after David's decision on the double-rename risk (po).
- 2026-10-23: after review: hint through `RenameHint`, unexpected-failure and navigation cases decided, ordering note (po).
- 2026-10-23: In Progress (builder).
- 2026-10-23: In Review (builder), PR #26.
