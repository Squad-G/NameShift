# NS-15: Rename, Revert and Reset buttons

Status: In Review
Depends on: NS-13, NS-52, NS-14, NS-9
Area: app

## Why
Run the batch, undo it, or start over, from the window.

## Acceptance criteria
1. Given no change in any name, then Rename is disabled.
2. Given conflicts (NS-52) or any item with an Error status (a rule error), then Rename is disabled and its hint says to fix them, matching the executor, which refuses such a plan (NS-8). This is the only place that rule is written.
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
- What Rename runs: exactly the preview on screen (`PreviewModel.Snapshot.Results` and `.Conflicts`); nothing is recomputed at click time. The executor's own run-time checks (NS-8) stay the safety net (tech-lead, 2026-10-18).
- Rename is enabled only when no action is running, the file list is loaded, the preview is current (no debounce waiting, no run in flight, and it is for the list's current batch), the last run did not fail, and the snapshot has at least one changed name, no conflict and no rule error. Written once, in `MainActions.CanRename` (criterion 2). While a new preview is pending the button is briefly disabled, by design (tech-lead, 2026-10-18).
- The hint is a visible label under the buttons (a tooltip does not show on a disabled button), also the Rename button's accessible description: `Fix the conflicts to rename.`, `Fix the rule errors to rename.` or `Fix the conflicts and rule errors to rename.`; nothing for "no change", "loading" or "busy" (tech-lead, 2026-10-18).
- Confirmation counts the names that change, not the selected items: 501 or more asks `Rename 1,234 files?` (Yes / No, Yes is the default), 500 or fewer never asks. It lives in the window model, not in `JournaledRenamer`, so the CLI never gets it (tech-lead default, 2026-10-18; po may overrule).
- If the preview changed while the confirmation was open, nothing runs and the status says `The new names changed. Check them and click Rename again.` (tech-lead, 2026-10-18).
- Locking: from the click (after the confirmation) until the folder has reloaded, the three buttons, the path bar and the whole split area are disabled, a marquee and `Renaming 12 files...` / `Reverting...` show, the wait cursor is on, and a user close is refused (`Wait until the rename finishes.`). Progress is indeterminate: the executor has no progress callback (tech-lead, 2026-10-18).
- Rename / Revert and the journal read after it run in one `Task.Run`; the result is applied on the UI thread. An exception escaping the renamer (a bug) says `Something went wrong. Check the files in the folder before you continue.`, reads the journal again and reloads the folder (tech-lead, 2026-10-18).
- After every Rename or Revert the current folder reloads (also after a refusal or a rollback), which clears the selection (NS-14). The rules are kept, so the New Name column shows them applied again to the new names (tech-lead default, 2026-10-18; po may overrule; question logged for David).
- Revert is enabled when no action is running and the last journal read says it can revert: after a batch, after a restart (NS-9:3), and after a partial revert or one that needed attention (NS-9:8). Disabled for None, Reverted, Interrupted, Unreadable, and until the first read finished. It acts on the journal's folder even when another folder is shown; its tooltip says `Undo the last rename: 12 names in D:\Photos`. No confirmation (tech-lead, 2026-10-18).
- The journal is read off the UI thread when the window is shown and after every Rename / Revert; a read applies only if no newer read or action started after it (a version counter) (tech-lead, 2026-10-18).
- Reset resets every panel that has something to reset (one `RulesChanged` at most), keeps the folder and the selection (criterion 6), never touches a file or the journal. Enabled when no action is running and a panel has something to reset (tech-lead, 2026-10-18).
- Messages: routine results go to the status bar only (criterion 9). A Warning dialog shows only when the user must look at specific items: items left away from their names (rename or revert) and Revert's skipped entries; each list is capped at 20 lines plus `...and N more.`. The status bar's tooltip carries the full text (tech-lead, 2026-10-18).
- Keyboard: no `AcceptButton` (Enter in a rule field never renames), no `CancelButton`, no mnemonics on the three buttons (Alt+R stays with Replace), no Ctrl+Z; the buttons are reached with Tab (tech-lead default, 2026-10-18; po may overrule).
- Test seam: `NameShift.Ui` has an `IBatchRenamer` with one adapter over `JournaledRenamer`; the real history folder is named only in `Program.cs`, tests use temp folders. `NameShift.Disk` only gains `InternalsVisibleTo NameShift.Ui.Tests`, so tests build real result objects through their validating constructors (tech-lead, 2026-10-18).
- `ResetAll` resets only the panels that have something to reset: a panel's `Reset` always raises `Changed`, so resetting an already default panel would start a preview run for nothing (builder, 2026-10-18).
- After an unexpected preview failure `PreviewModel.IsCurrent` is false (the snapshot is `Empty`, not the list's batch); Rename is off either way through `HasFailed` (builder, 2026-10-18).
- In Revert's dialog, a skipped entry is shown by its full path (where the rename left it) and the item holding its old name by its file name; each list (items needing attention, skipped entries) is capped at 20 lines on its own (builder, 2026-10-18).

## Log
- 2026-10-18: created (po).
- 2026-10-18: set Ready, first round of the minimal usable slice (po).
- 2026-10-18: criterion 2 extended: rule errors (any item with an Error status) also disable Rename (tech-lead, from NS-8's rule-error decision).
- 2026-10-18: In Progress, branch feature/NS-15-buttons (builder).
- 2026-10-18: In Review, PR #23 (builder): Reset, Revert and Rename buttons at the bottom right; Rename runs the preview on screen through the journaled renamer off the UI thread with the window locked, then reloads the folder; Revert undoes the journal's last batch the same way; Reset clears the rule panels.
