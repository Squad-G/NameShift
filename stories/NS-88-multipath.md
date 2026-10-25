# NS-88: Start on several files: one folder, those items selected

Status: In Review
Depends on: NS-47, NS-14
Area: app

## Why
`NameShift.exe "D:\Pics\a.jpg" "D:\Pics\b.jpg"` opens `D:\Pics` with exactly those rows selected, so Rename touches only
what the user picked. This is the first piece of the Explorer entry (NS-49); a single window for many launches is NS-89.

## Acceptance criteria
Logic in `NameShift.Ui` (`OpenTarget`, `FileListModel`) with tests on net10.0 and net48 (the fake folder source of NS-47);
`MainForm` only calls it. Names are indicative.
1. Given `OpenTarget.PathArguments(args)`, then it returns every path argument in order, skipping options (a word starting
   with `-`) and empty or white-space arguments, like `FirstPathArgument` (which stays and returns the first of them).
2. Given exactly one path argument, then everything is as NS-47 (a folder opens; a file opens its folder and selects it;
   messages as before).
3. Given two or more path arguments, then each is resolved with NS-47's rules (quotes, `/`, relative to the current
   directory, invalid characters, the closing-quote trap). The folder to open is the parent of the first path that
   exists; a path that is a folder is selected in its parent here, never opened. The selection is every existing path whose
   parent is that same folder (compared ignoring case and a trailing backslash); duplicates count once.
4. Given paths in other folders, paths that do not exist and paths that are invalid, then they are not selected and the
   status bar says `N items were not selected: not found or not in {folder}.` (new string; none, nothing is said). Given
   none exists, then the first path's NS-47 message shows and the window opens as usual.
5. Given `FileListModel` selection by names (indicative `SelectNames(IEnumerable<string>)`), then it selects the rows of the
   shown folder (`PathInput.SameFolder(item.FolderPath, FolderPath)`, as `FindRow` does; never a row of a sub-folder) whose
   name equals one of them (ordinal, ignoring case), returns how many were selected, selects nothing for a name the list
   does not show (hidden or filtered out), and scrolls the first selected row into view. A test row with Sub-folders on and
   a root `a.jpg` next to `sub\a.jpg`: asking for `a.jpg` selects only the root one (Rename must never touch what was not
   picked). The `N of M files` count shows N.
   It replaces the current selection. With 20,000 names against 20,000 items, it is linear (a name to row map, never a scan
   per name), tested by a test that completes and selects all of them.
6. Given the selection is made on the loaded list, then it happens after the load lands (as NS-47's `FileName`), and a click
   in the tree or a typed path meanwhile wins (the ticket of NS-47).
7. Given paths with spaces, non-ASCII characters, a file in a drive root (`D:\a.jpg` opens `D:\`) and a network path,
   then they are handled (fake source).
8. Given a command line over about 8,000 characters (hundreds of paths), then all of them are used (the process gets up to
   32,000 characters from Windows; beyond that, NS-89 and NS-49 decide).
9. By hand, written in the PR: `NameShift.exe "D:\Pics\a.jpg" "D:\Pics\b.jpg"` selects the two rows and Rename changes only
   those; the same with a file and a folder; with `Sub-folders` on, rows of the root are still selected.

## Out of scope
- One window for many launches and the hand-over from a second process (NS-89).
- Dropping several items on the window (it still uses the first, NS-47).
- The registry entry (NS-49).

## Decisions
- Two paths where one is a folder? **Decision:** with two or more paths a folder is an item to select, not a place to open;
  with one path it opens (NS-47), which is what a right-click on a folder sends (po, 2026-10-25).
- Paths of several folders? **Decision:** the first existing path decides the folder and the others are reported and ignored:
  Explorer never sends a mixed selection, and a half-guess must not select files the user did not mean (po, 2026-10-25).

## Log
- 2026-10-25: created from NS-49's split (po).
- 2026-10-25: built: ResolveMany, SelectNames, status line for unselected items; singular string NotSelectedOne added (tech lead).
