# NS-82: Open a folder from the file list

Status: Ready
Depends on: NS-10, NS-51, NS-78
Area: app

## Why
A folder row in the file list does nothing when double-clicked. A user expects to go into it, as in any file manager
(found by David while testing, 2026-10-24).

## Acceptance criteria
Logic in `NameShift.Ui` with tests on net10.0 and net48; `FileList` only raises an event, `MainForm` wires it. Starting
point: `FolderNavigator` (`NavigateToTypedPathAsync`, `SelectFromTree`, its version counter), `PathInput`,
`RenameItem.FolderPath` / `Name` / `Kind`, the `PathBarResult` values, and the status texts `PathNotFound`,
`PathNotAFolder`, `PathInvalid`. Prior art: none; `FileList` has no double-click or Enter handling today (only Ctrl+A).
1. Given a folder row, when the user double-clicks it, then the app opens that folder: the path box, the tree selection
   and the list follow, exactly as if it was picked in the tree (`FolderChanged`; the status bar is cleared).
2. Given a focused row (one or several selected), when the user presses Enter, then the same happens for the focused
   row if it is a folder. Enter on a file row does nothing (no rename, no open, no beep text). Double-click on a file row
   does nothing. Double-click on the header or on empty space does nothing. Enter in the filter or path box is unchanged.
3. The target is the row's own full path: a new `NameShift.Ui` function (for example `FolderRowTarget.Of(RenameItem)`)
   returns `Path.Combine(item.FolderPath, item.Name)` for a folder and null for a file, so a folder of a sub-level
   (Sub-folders box ticked, NS-78) opens that folder and not a path relative to the folder shown. Tests: a folder in the
   root; a folder in a sub-level (`FolderPath` differs from the root); a file (null); a name with spaces and non-ASCII
   letters; a name with `%` (`100%`, `%TEMP%`) returned unchanged.
4. The path of a row is exact, so it is never treated as typed text: a new entry point on `FolderNavigator` (for example
   `NavigateToFolderAsync(string fullPath)`) navigates without `%VAR%` expansion, without trimming or stripping quotes
   (as the command-line and drop paths do with `expandVariables: false`). It shares the version counter, the disk
   check off the UI thread and the `Change` step with `NavigateToTypedPathAsync`, so a later navigation supersedes it.
   Tests (fake `IFolderSource`) that would fail without this story: a folder named `100%` opens that folder; a folder
   named `%TEMP%` opens `...\%TEMP%` and not the expansion (the fake records the path it was asked about); a name with
   a leading space or a quote is passed unchanged (a trailing space is not tested: `GetFullPath` on net48 strips it); exists gives `Navigated` and one `FolderChanged`; the shown
   folder gives `Unchanged` and no second `FolderChanged`; a newer navigation started meanwhile gives `Superseded`; a path
   `Path.GetFullPath` rejects (too long on net48) gives `Invalid`, never an exception. The existing tests of the typed
   path stay as they are and are not repeated.
5. Given the target no longer exists, is now a file, or is rejected, then the window stays where it is and the status bar
   (`MainForm.ShowStatus`) says it with the folder's full path as `{0}`: `NotFound` uses `PathNotFound`, `NotAFolder`
   `PathNotAFolder`, `Invalid` `PathInvalid`, and `Empty` (cannot happen from a row) the same as `Invalid`; `Superseded`
   and `Unchanged` say nothing. No `MessageBox`. `MainForm.ShowPathResult` is not reused: it is tied to the path box
   (it selects the typed text and its `{0}` is the typed text); a Ui function turns a `PathBarResult` and a path into the
   status text or null, with a test per result (and the existing path-box texts unchanged).
6. Given a Rename or Revert is running (NS-15), then the window is locked as today (`_mainSplit` is disabled), so the
   double-click does nothing (by hand).
7. Given the selection, the sort, the filter row and the Sub-folders box, then opening a folder behaves as picking one in
   the tree does today (the existing folder change handling): no new rule.
8. There is no Back or Up history in the app today, so none is added here. (A "Go up" is a possible later story.)
9. By hand, written in the PR: double-click a folder; Enter on a folder; Enter on a file; a folder deleted in Explorer
   after listing then double-click (status bar text); a folder named `100%`; with Sub-folders ticked, double-click a folder
   of a sub-level; the tree follows.

## Out of scope
- Opening a file with its program; Back, Up, or an Alt+Up shortcut; Explorer context menu; opening a folder in Explorer.

## Decisions
- Enter on a file row? **Decision:** nothing: a renamer must not launch files, and an accidental Enter must have no
  effect (po, 2026-10-24).
- A vanished folder? **Decision:** a status-bar message with the existing path texts, no dialog (CLAUDE.md: routine
  feedback goes to the status bar) (po, 2026-10-24).
- Reuse the typed-path navigation as is? **Decision:** no: it expands `%VAR%`, trims and strips quotes, which would open
  the wrong folder for a folder literally named `%TEMP%`. A small non-expanding entry point is added to the navigator and
  shares its version and check code (reviewer finding, po, 2026-10-24).
- Back/Up? **Decision:** not in this story: none exists, and one rule per story (po, 2026-10-24).

## Log
- 2026-10-24: created from David's test report; checked `FileList`, `FolderNavigator`, `MainForm.OnFolderChanged` and
  the stories (no prior decision); set Ready, first after NS-78 (po).
- 2026-10-24: PR #82 review applied: non-expanding entry point and its tests, status texts for every result, NS-78
  dependency (po).
