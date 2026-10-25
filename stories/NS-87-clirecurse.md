# NS-87: Command line: --recurse

Status: In Progress
Depends on: NS-41, NS-74, NS-78
Area: app

## Why
Rename the files of a whole tree from a script, as the Sub-folders box does in the window.

## Facts checked against dev (2026-10-25, 3dca426, NS-41 merged)
- `CommandRunner.RunApply` calls `_files.ListEntries(folder, false, CancellationToken.None)` (one place), then
  `Problem` check, then `FileListOrder.Sort(listing.Items).Where(options.Filter.Includes)`. With `options.Filtered` and
  no item left it writes `Strings.NoFilesMatch` (not with `--quiet`) and returns 0. Then `RenamePlanner.Plan(rules,
  items, listing.ExistingNamesByFolder, ...)`. There is no warning path yet; `_error` is the error writer.
- Options are read by `ApplyOptionReader` classes (`ApplyReaders.Create()`), their values go into `ApplyDraft`, and
  `ApplyDraft.Build` makes `ApplyOptions` (its constructor takes the filter and `filtered`; `--recurse` is a new
  parameter there). `RunSwitchesReader` is the model for a plain switch (`TakeSwitch` gives `RepeatedOption`).
- `ParseApply` reads the folder mask (`FolderMask.Split`) after the readers; the mask ends in the filter, so it applies to
  the names of every folder.
- Output lines are written in `WriteChanged`, `WriteUnchanged` (both `result.Item.Name`) and `Blocked`
  (`items[i].Name`): these three are the places that show a name.
- `DiskFileListSource.ListEntries(folder, true, token)`: tree order, hidden and system entries included (the filter
  decides), reparse points listed but not entered and not counted as skipped, unreadable sub-folders counted in
  `FileListing.SkippedFolders` and skipped, the chosen folder failing gives `Problem`. `FileListOrder.Sort` keeps the
  items grouped by folder in tree order. `ExistingNamesByFolder` is per folder.
- `SkippedFoldersNotice.Text(int, CultureInfo)` is static: null for 0, `1 folder could not be read`, else
  `N folders could not be read` (N0). `FileListText.RelativeFolder(root, item)`: `""` for the root, `sub\deep` below it.
- The journaled renamer and `RenameOrder` already order a batch with a folder and items inside it, and Revert undoes it
  (NS-9, NS-74).
- `FilterReader` has no `--recurse`; the Numbering reader has no "restart in each folder" option (the window has one).
- NS-86 also edits the apply path of `CommandRunner` (the photo-date fill after the filter): build the two one after the
  other. NS-85 does not touch the command line and can be built at the same time as this story.

## Acceptance criteria
Logic in `NameShift.Cmd`, tests on net10.0 and net48. `--recurse` is a switch read by a reader of its own (or
`RunSwitchesReader`), carried by `ApplyOptions`, and `CommandRunner` passes it to
`IFileListSource.ListEntries(folder, includeSubfolders, token)`.
1. Given `--recurse`, then the files of every sub-folder are listed with the folder's own and renamed in place, in the
   window's tree order (each folder's entries together, folders in natural name order). Given no `--recurse`, then
   `ListEntries` is called with false, as today. Given `--recurse` twice, then `RepeatedOption`.
2. Given reparse points (junctions, links), then they are not entered (the listing's rule, NS-74); the same folder is never
   listed twice. A fake source tests the call; a real temp folder with a junction made in the test (`mklink /J` needs no
   admin; skipped with a note when the process fails; the junction's target is an absolute path inside the temp root)
   tests the rule.
3. Given `--recurse`, then each output line names the item by its path relative to the chosen folder: `old -> new` is
   `sub\a.jpg -> b.jpg` (the new name only on the right), `sub\a.jpg: unchanged`, and the conflict and error lines
   `sub\a.jpg: ...`. A file of the chosen folder shows its plain name. Without `--recurse` the lines are as today. The
   relative path is `FileListText.RelativeFolder` + `\` + the name (`WriteChanged`, `WriteUnchanged` and `Blocked` share
   one helper). The same lines are used by `--dry-run`, `--verbose` and a blocked batch.
4. Given the same new name for files in two different folders (`x.txt` in the root and in `sub`), then there is no
   conflict (names are compared per folder, as in the window); given two in the same folder, the usual conflict.
5. Given sub-folders that cannot be read, then one line (`SkippedFoldersNotice.Text`: `1 folder could not be read` or
   `N folders could not be read`) goes to the error stream right after the listing and before any other result line (also
   with `--dry-run`, `No files match.` and `Nothing to rename.`), the rest is renamed, and the exit code is not changed
   (still 0 when the rest succeeds). `--quiet` does not hide it. Given none skipped, nothing is written. Given no
   `--recurse`, the line is never written (tested with a fake source that returns a count, since the real source
   reports 0 then). Tests use a fake source with `SkippedFolders` 1 and 2, with `--quiet`, with `--dry-run`, and check
   the stream (output stays free of it). Never a real drive root: it always counts `System Volume Information` and
   `$RECYCLE.BIN`, so the line appears on every `D:\ --recurse` run (expected, not a bug).
6. Given `--recurse` with `--include-folders`, then a batch that renames a folder and files inside it works in one batch
   through the real journal, and `undo` restores both (temp-folder test). Given `--recurse` with `--folders-only`, then
   the folders of every level are the items (shown by relative path), the files are not. Given `--recurse` alone, then
   folders are not renamed (files only, as without it).
7. Given a filter (NS-41), then it applies to the items of every folder (mask, regex, lengths, hidden), and a mask in the
   folder argument (`D:\Pics\*.jpg`) works the same. A hidden or system file in a visible folder follows the filter as
   without `--recurse`. `No files match.` is written when nothing in the whole tree matches.
7a. Given `--recurse` without `--include-hidden`, then an item whose folder is, or is under, a hidden or system folder of
   the walk (at any depth) is left out, whatever its own attributes (`repo\.git\config` is never planned). The folder
   items of the listing carry the attributes, so this is computed from the listing. Given `--include-hidden`, then those
   items are in. Given no `--recurse`, nothing changes. Tests: a fake source with a hidden folder `h` holding a plain
   file and a plain folder holding a plain file (the first is left out, the second kept), a system folder, a hidden
   folder two levels up, the same with `--include-hidden`, and, when only such items are left, `No files match.` if a
   filter option or a folder mask was also given and `Nothing to rename.` otherwise (the `Filtered` rule of NS-41 is not
   changed: `--recurse` alone does not set it). Only the folders found below the chosen folder are tested, never the
   chosen folder itself: a hidden folder named on purpose still works (`apply C:\Users\me\AppData --recurse ...`,
   `apply D:\repo\.git --recurse ...`); a test gives a hidden chosen folder with a plain file and a plain sub-folder, and
   both files are planned. A real temp tree with a hidden folder confirms it.
8. Given Ctrl+C during the listing, then nothing has been renamed (the listing and the plan come before the first rename);
   checked by hand on a large tree, written in the PR.
9. Given the apply help, then it names `--recurse` in the options list (help test lists it), with a line of at most 79
   columns such as `--recurse                  Also the sub-folders (not hidden ones).`; if it does not fit, say hidden
   folders are left out in a second line under it. The `--include-hidden` line becomes
   `--include-hidden           Also hidden and system items and folders.` (70 columns), since with `--recurse` it also
   enters hidden folders.
10. Given a tree of 10,000 files in 100 folders, then a dry run through the CLI completes; the PR writes down the time (not
    asserted).

## Out of scope
- A depth limit; following reparse points; listing folders only to a given depth.
- A "restart numbering in each folder" option (the window has one): with `--recurse` the numbers run through the whole
  list, as the window's default. A separate story if wanted.
- A non-zero exit code or `--strict` for skipped folders.
- Relative paths in the run-failure details (`ActionText.ForRename`, a bare name, shared with the window) and in
  `undo --dry-run` (bare names): they stay as they are.

## Decisions
- Relative names in the output? **Decision:** yes: the same name can exist in many folders, and the relative path is what
  the window's Folder column shows (po, 2026-10-25).
- Unreadable sub-folders? **Decision:** rename the rest and warn on the error stream, like the window's status line; the
  user loses nothing and sees the count (po, 2026-10-25).
- Warning text and stream? **Decision:** the window's own text through `SkippedFoldersNotice.Text` (no new string), on the
  error stream, once, right after the listing: one wording for both programs, and a script that reads the output stream
  stays clean (po, 2026-10-25).
- Exit code with skipped folders? **Decision:** unchanged (0, 1 to 4 as the batch decides): the codes are documented and a
  script that wants strictness reads stderr; nothing is lost because the skipped folders are untouched (po, 2026-10-25).
- `--recurse` with `--folders-only` and `--include-folders`? **Decision:** they work at every level, with the batch going
  through the journal in one batch; `--recurse` alone stays files only, so a tree is never renamed by accident (po,
  2026-10-25).
- The folder mask with `--recurse`? **Decision:** the mask filters names in every folder and never limits which folders
  are walked: it is the same filter as the window's, applied to the whole list (po, 2026-10-25).
- Name conflicts across folders? **Decision:** compared per folder, as the window and the planner do; two folders may
  both get `x.txt` (po, 2026-10-25).
- Relative path display? **Decision:** the Folder column's text plus `\` plus the name, never the full path; the root's
  items show the plain name, so a run without sub-folders listed looks like today (po, 2026-10-25).
- Hidden folders with `--recurse`? **Decision (Lead, 2026-10-25):** without `--include-hidden`, everything inside a hidden
  or system folder, at any depth, is left out, so `apply D:\repo --recurse ...` never touches `.git` or similar;
  `--include-hidden` brings it in. The window keeps entering hidden folders because it always previews first; the CLI is
  stricter because it renames without a preview (po, 2026-10-25).
- Numbering across folders? **Decision:** it runs through the whole list in tree order (the window's default); no new
  option now (po, 2026-10-25).

## Log
- 2026-10-25: split from NS-41 (po).
- 2026-10-25: re-checked against dev (306e581) while NS-41 is not yet built: facts recorded, `SkippedFoldersNotice.Text`
  signature noted, build order with NS-86 noted; stays Draft until NS-41 is merged (po).
- 2026-10-25: NS-41 merged (3dca426). Every criterion re-checked against the merged code: facts refreshed, `--folders-only`
  and `--include-folders` with `--recurse`, the mask, the warning text and stream, cross-folder conflicts and the
  help line decided; nothing needs David. Ready (po).
- 2026-10-25: review of PR #102: hidden folders left out with `--recurse` unless `--include-hidden` (criterion 7a), nits
  folded in (out of scope, fake-source tests, absolute junction target) (po).
- 2026-10-25: PR #102 re-review nits folded in by the tech lead: 7a names the message when only hidden-folder items are
  left, the chosen folder itself is never tested (with a test), and the `--include-hidden` help line (tech-lead).
- 2026-10-25: started (builder).
