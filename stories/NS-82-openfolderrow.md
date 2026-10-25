# NS-82: Open a folder from the file list

Status: Ready
Depends on: NS-10, NS-51
Area: app

## Why
A folder row in the file list does nothing when double-clicked. A user expects to go into it, as in any file manager
(found by David while testing, 2026-10-24).

## Acceptance criteria
Logic in `NameShift.Ui` with tests on net10.0 and net48; `FileList` only raises an event, `MainForm` wires it. Starting
point: `FolderNavigator.NavigateToTypedPathAsync` (checks the disk off the UI thread, returns a `PathBarResult`),
`RenameItem.FolderPath`, `RenameItem.Name`, `RenameItem.Kind`, and the status texts `PathNotFound` and `PathNotAFolder`.
Prior art: none; `FileList` has no double-click or Enter handling today.
1. Given a folder row, when the user double-clicks it, then the app opens that folder: the path box, the tree selection
   and the list follow, exactly as if it was picked in the tree (`FolderChanged`; the status bar is cleared).
2. Given a focused row (one or several selected), when the user presses Enter, then the same happens for the focused
   row if it is a folder. Enter on a file row does nothing (no rename, no open, no beep text). Double-click on a file row
   does nothing. Double-click on the header or on empty space does nothing. Enter in the filter or path box is unchanged.
3. Given the Sub-folders box is ticked (NS-78) and the row is a folder of a sub-level, then the target is that folder's
   own full path: `Path.Combine(item.FolderPath, item.Name)`, not a path relative to the folder shown. A new
   `NameShift.Ui` function (for example `FolderRowTarget.Of(RenameItem)`, returns the path or null for a file) is tested:
   a folder in the root, a folder in a sub-level (`FolderPath` differs from the root), a file (null), a name with spaces
   and non-ASCII letters.
4. Given the target no longer exists, is now a file, or cannot be reached, then the window stays where it is and the
   status bar (`MainForm.ShowStatus`) says it with the existing `PathNotFound` or `PathNotAFolder` text; no `MessageBox`.
   The check runs off the UI thread (it reuses `NavigateToTypedPathAsync`, so a second click, or the user moving
   elsewhere meanwhile, supersedes it: `Superseded` says nothing). Tests with a fake `IFolderSource`: exists (navigated,
   `FolderChanged` once), gone (`NotFound`, no change), a file (`NotAFolder`), same folder as shown (`Unchanged`, no
   second `FolderChanged`).
5. Given a Rename or Revert is running (NS-15), then the window is locked as today (`_mainSplit` is disabled), so the
   double-click does nothing (by hand).
6. Given the selection, the sort, the filter row and the Sub-folders box, then opening a folder starts fresh as picking
   one in the tree does (selection cleared, sort kept, filters as the existing folder change handles them): no new rule.
7. There is no Back or Up history in the app today, so none is added here. (A "Go up" is a possible later story.)
8. By hand, written in the PR: double-click a folder; Enter on a folder; Enter on a file; a folder deleted in Explorer
   after listing then double-click; with Sub-folders ticked, double-click a folder of a sub-level; the tree follows.

## Out of scope
- Opening a file with its program; Back, Up, or an Alt+Up shortcut; Explorer context menu; opening a folder in Explorer.

## Decisions
- Enter on a file row? **Decision:** nothing: a renamer must not launch files, and an accidental Enter must have no
  effect (po, 2026-10-24).
- A vanished folder? **Decision:** a status-bar message with the existing path texts, no dialog (CLAUDE.md: routine
  feedback goes to the status bar) (po, 2026-10-24).
- New navigation code? **Decision:** no: reuse the typed-path navigation, which already checks the disk off the UI
  thread and handles races (po, 2026-10-24).
- Back/Up? **Decision:** not in this story: none exists, and one rule per story (po, 2026-10-24).

## Log
- 2026-10-24: created from David's test report; checked `FileList`, `FolderNavigator`, `MainForm.OnFolderChanged` and
  the stories (no prior decision); set Ready, first after NS-78 (po).
