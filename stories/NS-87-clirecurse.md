# NS-87: Command line: --recurse

Status: Draft
Depends on: NS-41, NS-74, NS-78
Area: app

## Why
Rename the files of a whole tree from a script, as the Sub-folders box does in the window. Second wave: set to Ready when
NS-41 is merged (it uses the filter in `CommandRunner`).

## Acceptance criteria
Logic in `NameShift.Cmd`, tests on net10.0 and net48. `CommandRunner` passes the option to
`IFileListSource.ListEntries(folder, includeSubfolders, token)` (today it always passes false).
1. Given `--recurse`, then the files of every sub-folder are listed with the folder's own and renamed in place, in the
   window's tree order (each folder's entries together, folders in natural name order). Given no `--recurse`, then
   `ListEntries` is called with false, as today. Given `--recurse` twice, then `RepeatedOption`.
2. Given reparse points (junctions, links), then they are not entered (the listing's rule, NS-74); the same folder is never
   listed twice. A fake source tests the call; a real temp folder with a junction made in the test (`mklink /J` needs no
   admin; skipped with a note when it cannot be made) tests the rule.
3. Given `--recurse`, then each output line names the item by its path relative to the chosen folder: `old -> new` is
   `sub\a.jpg -> b.jpg` (the new name only on the right), `sub\a.jpg: unchanged`, and the conflict and error lines
   `sub\a.jpg: ...`. A file of the chosen folder shows its plain name. Without `--recurse` the lines are as today. The
   relative path is `FileListText.RelativeFolder`, the window's Folder column.
4. Given the same new name for files in two different folders (`x.txt` in the root and in `sub`), then there is no
   conflict (names are compared per folder, as in the window); given two in the same folder, the usual conflict.
5. Given sub-folders that cannot be read, then one line `N folders could not be read` (one: `1 folder could not be read`,
   `SkippedFoldersNotice.Text`) goes to the error stream before the plan's result, the rest is renamed, and the exit code
   is not changed; `--quiet` does not hide it (NS-41 decision).
6. Given `--recurse` with `--include-folders`, then a batch that renames a folder and files inside it works in one batch
   through the real journal, and `undo` restores both (temp-folder test).
7. Given a filter (NS-41), then it applies to the items of every folder, and a mask in the folder argument works the same.
8. Given Ctrl+C during the listing, then nothing has been renamed (the listing and the plan come before the first rename);
   checked by hand on a large tree, written in the PR.
9. Given the apply help, then it names `--recurse`.
10. Given a tree of 10,000 files in 100 folders, then a dry run through the CLI completes; the PR writes down the time (not
    asserted).

## Out of scope
- A depth limit; following reparse points; listing folders only to a given depth.

## Decisions
- Relative names in the output? **Decision:** yes: the same name can exist in many folders, and the relative path is what
  the window's Folder column shows (po, 2026-10-25).
- Unreadable sub-folders? **Decision:** rename the rest and warn on the error stream, like the window's status line; the
  user loses nothing and sees the count (po, 2026-10-25).

## Log
- 2026-10-25: split from NS-41 (po).
