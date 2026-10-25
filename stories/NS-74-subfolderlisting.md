# NS-74: Sub-folders: recursive, cancellable listing

Status: In Review
Depends on: NS-33, NS-66, NS-73
Area: app

## Why
List the files of all sub-folders and rename them in place, with a listing that can be cancelled and never follows a
junction. This is the logic only (no control, no column): the box and the column are NS-78. (Part of the original NS-33.)

## Acceptance criteria
`NameShift.Ui` only (plus the one call in the command line); tests on net10.0 and net48. The real disk is used only where
a criterion says so. Names are indicative. Starting point (merged NS-33): `IFileListSource.ListEntries(string)`,
`DiskFileListSource`, `FileListing` (`Ok(items)`, `Failed(problem)`, `ExistingNamesByFolder`), `FileListModel`
(`LoadAsync`, `SetFilterAsync`, one shared `_version`, one `CancellationTokenSource` for the work in flight, `Dispose`),
`FileListOrder.Sort`, `FilterPanelModel`.

Seam and contract:
1. `DiskFileListSource` takes an injectable directory enumerator through an internal constructor (the public parameterless
   one stays, so `Program` and `CommandRunner.ForDisk` do not change): an interface with `Enumerate(path)` returning
   entries with name, kind, `IsReparsePoint`, the three `ItemAttributes`, size and times. It may throw when called or
   while it is enumerated, with the errors a real disk throws. The default wraps `DirectoryInfo.EnumerateFileSystemInfos`.
   `ItemAttributes` (Core) is not changed: a reparse point is a property of the entry, not of the item. Tests of
   unreadable folders, depth, order and cancellation use a fake of it.
2. `IFileListSource.ListEntries(folderPath, includeSubfolders, cancellationToken)` replaces the one-argument method (no
   overload left). The contract "never throws" becomes "never throws except `OperationCanceledException` when the token is
   cancelled". `DiskFileListSource`'s catch-all rethrows cancellation (it must not turn it into a `Failed` listing).
   `FileListModel` passes the token of the load in flight; its existing handler (`catch (OperationCanceledException) when
   cts.IsCancellationRequested`) already means "superseded: returns false, no `Failed` state, no message"; an
   `OperationCanceledException` with a token that is not cancelled still ends as `Failed` (a test). A model that is
   disposed returns false from `SetIncludeSubfoldersAsync` and stores nothing (as `SetFilterAsync`). The other caller, the
   command line (`CommandRunner`), passes `false` and `CancellationToken.None`; if NS-43 is merged first, the call is the one
   in its merged code.
3. `FileListing` gets `SkippedFolders` (int, 0 by default and for a failed listing; `Ok(items, skippedFolders = 0)`), and
   `FileListModel` exposes it as `SkippedFolders` (0 until a load lands, while loading and after a failure).
4. `FilterPanelModel.IncludeSubfolders` (default false, part of `IsDefault` and `Reset`, one `Changed` per real change, none
   for the same value). It does not rebuild `Filter`: `Filter` is the same instance after the change, so no filter run
   starts for it. `Reset()` (which rebuilds `Filter` every time today) rebuilds it only when a filter value changed, never
   for the box alone. Tests: `ReferenceEquals(Filter)` before and after setting the box; before and after a `Reset()` that
   changes only the box (same instance, one `Changed`); a new instance after a `Reset()` that also changes a filter value.
5. `FileListModel.IncludeSubfolders` (false by default, read-only property) and `SetIncludeSubfoldersAsync(bool)`:
   the same value returns false and does nothing; otherwise the value is stored and, when a folder is shown
   (`FolderPath` is not null), the same folder is loaded again (state `Loading` then `Loaded`, `Changed` as for any load);
   with no folder yet it is only stored and the first `LoadAsync` uses it. Every `LoadAsync` reads the value when it
   starts, so the reload after a Rename or Revert (`MainActions`) and a folder change keep it. The load keeps the sort
   and the filter requested (same folder: as any reload, NS-66; the filter is applied when it lands, NS-33:7) and
   clears the selection, as every load does. The window, not the model, ties `FilterPanelModel` to it (NS-78).

Listing:
6. Given `includeSubfolders` false, then the listing is the folder alone, as today (the existing tests pass unchanged apart
   from the new parameters).
7. Given a fake tree `root\a.txt`, `root\x\b.txt`, `root\x\y\c.txt`, then every file and folder is listed with its own
   `FolderPath` (`root`, `root\x`, `root\x\y`), and `ExistingNamesByFolder` has one key per folder that has an entry, hidden
   and system entries included. A folder with no entry has no key, which the duplicate check reads as "no names".
8. Given an entry that is a folder and a reparse point (the fake says so), then it is listed as a folder item but never
   entered. A loop (`root\loop` pointing at `root`) ends. A file that is a reparse point is listed like any file. One test uses
   a real junction made with `cmd /c mklink /J` in a temp tree (no administrator rights needed): if it cannot be created
   the test fails with the command's output (it does not skip), checks that the junction's target is not listed a second
   time through it, and removes it with `Directory.Delete(path, false)` (never recursive: that would delete the target).
9. Given a sub-folder whose enumeration throws (`UnauthorizedAccessException`, `PathTooLongException`,
   `DirectoryNotFoundException` as if removed meanwhile, `IOException`), whether at the call or midway, then it is skipped,
   the entries read before the error are kept, the rest of the tree is listed, and `SkippedFolders` counts it once. The
   sub-folders already discovered in a folder that fails midway are still entered. That folder's names in
   `ExistingNamesByFolder` are partial: safe (NS-8 re-checks every target on disk and never replaces), but the preview can
   say OK and the Rename then fail and roll back. The root
   failing is the usual folder problem (`Failed`, `FolderProblem` as today).
10. Given a fake tree 5,000 folders deep with one file in each, then it is listed without a stack overflow (the walk keeps its
    own stack, never recursion) and the test completes in a few seconds. The test must fail, not just be slow, on a
    quadratic walk (a time limit of a few seconds, with paths of about 10 KB at the deepest).
11. Order, decided in the source and kept by the sort. The source returns the entries grouped by folder in tree order: the
    root first, then each sub-folder right after its parent's entries, siblings in the natural name order (`x`, `x\y`, `x2`,
    never `x`, `x2`, `x\y`). `FileListOrder.Sort` groups the items by `FolderPath` (ignoring case) in the order each folder
    first appears in the input, then orders each group as one folder (folders first, then the column's order, ties by natural
    name, NS-66), whatever the column and direction: a descending sort reverses the rows of a group, never the groups.
    With one folder the result is what it is today (the existing tests pass). Tests: `DiskFileListSource` over a fake tree
    returns the order above; `FileListOrder.Sort` over two interleaved folders gathers them; a descending size sort keeps
    the root first; `SortBy` then a filter keeps the group order. Guard: 10,000 items in 500 folders sort in under 1 second.
12. Given `FindRow(name)` (NS-47), then it finds an item of the shown folder only (an item whose `FolderPath` is the model's
    `FolderPath`, ignoring case), never a same-named file of a sub-folder.

Cancel:
13. Given a new `LoadAsync`, a change of `IncludeSubfolders` or `Dispose`, then the walk in flight is cancelled: the source
    receives the token of that load and checks it at least once per folder and once per 1,000 entries. Observable tests:
    a fake enumerator cancels the token's source after k entries of one folder holding many more, and the test asserts the
    source asked for at most k + 1,000 more entries and the listing throws `OperationCanceledException`; another cancels
    inside folder 1 and asserts folder 2 is never enumerated; a fake that blocks until the token is cancelled and then
    throws ends the load as superseded. The walk runs off the UI thread (the
    existing `Task.Run`). The superseded load returns false and applies nothing. The real source called with a token
    already cancelled throws `OperationCanceledException` at once. There is no separate Cancel button or method: unticking
    Sub-folders (NS-78) or opening another folder is the cancel.

Relative folder and rename:
14. `FileListText.RelativeFolder(rootFolder, item)` returns `""` for an item of the root and the path under the root for a
    deeper one (`sub\deep`), ignoring case and trailing separators (`C:\Pics`, `c:\pics\a\b` gives `a\b`; `C:\` as the
    root works); an item outside the root returns its full folder path.
15. Given a batch with files of two folders in a real temp tree, then each is renamed in its own folder through the usual
    journal (the batch lists both folders) and Revert restores both. `a.txt` in `x` and `a.txt` in `y` both renamed to
    `b.txt` is not a conflict; two `b.txt` in one folder are, and `x\a.txt` renamed to a name that exists in `x` is
    `TargetExists` even when that file is hidden or filtered out.
16. Given a Rename of items in several folders, then `MainActions.FolderContentsChanged` lists every distinct folder of the
    batch (ignoring case) and the shown folder, so the folder tree refreshes the nodes of renamed sub-folders (a test with a
    fake renamer: a batch from `root` and `root\x` gives both). With one folder it lists that folder, as today.

## Out of scope
- The controls and the Folder column (NS-78); following junctions; a listing cap; a Cancel button; a progress count.
- A folder and a file inside it in the same batch: the executor orders the moves (NS-8) but `PathLengthCheck` measures only
  each item's own new path, so a child path that becomes too long after its parent is renamed is not predicted. Accepted
  as in NS-7 ("folder only for now"); the rename call then fails and the batch is rolled back, never half done.
- Command-line recursion (NS-41).

## Decisions
- Cycles through junctions? **Decision:** reparse points are never followed (po, 2026-10-18).
- Order with sub-folders? **Decision:** grouped by folder, root first, so numbering (and NS-60's restart per folder) sees each
  folder's files together (po, 2026-10-24).
- Who decides the folder order? **Decision:** the source (a tree walk with sorted siblings), and the sort only keeps the
  groups in the order they arrive: comparing whole paths segment by segment in the sort costs O(depth) per comparison and
  is too slow for a 5,000-deep tree (po, 2026-10-24).
- Unreadable sub-folder? **Decision:** skip it and say how many; the duplicate check still sees every name of every folder
  read (po, 2026-10-24).
- Listing cap? **Decision:** none; cancel and a responsive window are the safety (po, 2026-10-24).
- Is the option kept when I change folder? **Decision:** yes during the run, off at start and on Reset (po, 2026-10-24).
- Where does the option live? **Decision:** `FilterPanelModel` holds what the box shows (with Reset and `IsDefault`), and
  `FileListModel` holds what was applied, since it is a listing parameter and not an item filter; the window ties them
  (NS-78) (po, 2026-10-24).
- Reparse-point folders (junctions, links, possibly cloud placeholder folders such as OneDrive's)? **Decision:** listed,
  never entered, silently, and not counted in `SkippedFolders`: following them risks loops and double renames. The PR says
  whether a real OneDrive folder carries the attribute; if it does, a follow-up counts them (po, 2026-10-24).
- A read blocked on a dead share? **Decision:** it cannot be cancelled, only superseded: its pool thread stays blocked until
  the disk answers, and its result is never applied (po, 2026-10-24).
- A Cancel method or button? **Decision:** none: the window is not locked while a folder loads, so opening another folder
  or unticking the box cancels, and a second way would need a third state (po, 2026-10-24).

## Log
- 2026-10-24: split out of NS-33 after the PR #53 review (seam, cancellation contract, segment order, no false claim about
  long child paths); Draft until NS-33 is merged (po).
- 2026-10-24: NS-33 merged. Criteria aligned with the merged `FileListModel` (shared version and token),
  `ExistingNamesByFolder`, `FilterPanelModel` and `FileListOrder`; order moved to the source, the model gets
  `IncludeSubfolders`/`SetIncludeSubfoldersAsync`, the tree refresh lists every folder; the controls moved to NS-78; set
  Ready (po).
- 2026-10-24: In Progress (builder).
- 2026-10-24: In Review (builder), PR #72.
- 2026-10-24: Possible follow-up from the PR #72 review: a sub-folder whose name ends in a dot or a space (created
  through `\\?\`) is read as another folder (`x.` reads `x`) after `Path.Combine` and the `DirectoryInfo` normalisation,
  so its entries can be listed under the wrong `FolderPath` (the Rename then fails and rolls back, no data lost); the walk
  could skip and count such names (builder).
