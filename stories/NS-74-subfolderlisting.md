# NS-74: Sub-folders: recursive, cancellable listing

Status: Draft
Depends on: NS-33, NS-66, NS-73
Area: app

## Why
List the files of all sub-folders and rename them in place, with a listing that can be cancelled and never follows a
junction. (Part of the original NS-33.) Draft until NS-33 is merged; then Ready.

## Acceptance criteria
Names are indicative. `NameShift.Ui` only; tests on net10.0 and net48. The real disk is used only where it says so.

Seam and contract:
1. `DiskFileListSource` takes an injectable directory enumerator (an interface with `Enumerate(path)` returning entries with
   name, kind, attributes, size and times, and able to throw the errors a real disk throws); the default one wraps
   `DirectoryInfo.EnumerateFileSystemInfos`. Tests of unreadable folders, depth and cancellation use a fake of it.
2. `IFileListSource.ListEntries(folder, includeSubfolders, cancellationToken)`. The contract "never throws" becomes "never
   throws except `OperationCanceledException` when the token is cancelled": `DiskFileListSource`'s catch-all rethrows
   cancellation (it must not turn it into a `Failed` listing) and `FileListModel` treats it as a superseded load (the load
   returns false, no `Failed` state, no message). The callers that change: `FileListModel` and the command line (NS-38,
   when merged; it passes `false` and `CancellationToken.None`).
3. `FilterPanelModel` gets `IncludeSubfolders` (default false, in `IsDefault` and `Reset`, one `Changed` when it changes).
   Changing it loads the current folder again (state `Loading` then `Loaded`) with the new value; the selection is cleared as
   for any load, and the sort and the other filter values are kept. App start and `Reset()` set it off.

Listing:
4. Given `IncludeSubfolders` false, then the listing is the folder alone, as today.
5. Given a fake tree `root\a.txt`, `root\x\b.txt`, `root\x\y\c.txt`, then every file and folder is listed with its own
   `FolderPath`, and `ExistingNamesByFolder` (NS-33) has one entry per folder read, hidden entries included.
6. Given a folder entry that is a reparse point (the fake reports the attribute), then it is listed as a folder item but never
   entered; a loop (`root\loop` pointing at `root`) ends. One test uses a real junction made with `cmd /c mklink /J` (no
   admin rights needed): if the junction cannot be created the test fails with the command's output (it does not skip); it
   checks that the junction's target is not listed twice.
7. Given a sub-folder whose enumeration throws (`UnauthorizedAccessException`, `PathTooLongException`,
   `DirectoryNotFoundException` as if removed meanwhile), then it is skipped, the rest is listed, and the listing's
   `SkippedFolders` count says how many. The root failing is the usual folder problem (`Failed`).
8. Given a fake tree 5,000 folders deep (a path longer than the system allows needs no real disk), then it is listed without a
   stack overflow, and the test completes in a few seconds (the walk uses its own stack or queue).
9. Order: items are grouped by folder, the root first, then each sub-folder in the natural order of its path compared segment
   by segment (split on `\`, each segment with the natural name comparer, a shorter path first when one is the prefix of the
   other), so a parent's children are contiguous: `x`, `x\y`, `x2` (not `x`, `x2`, `x\y`). Inside a group the order and the
   NS-66 sort are those of one folder (folders first, then the column's order). With `IncludeSubfolders` false the order is
   unchanged.
10. Given `FindRow(name)` (NS-47), then it finds a root-folder item only.

Cancel:
11. Given a new load, a change of `IncludeSubfolders`, or `Cancel()`, then the running listing is cancelled: the source
    receives a cancelled token and checks it at least once per folder and once per 1,000 entries (a test with a fake that
    counts the checks and a fake that blocks until cancelled); the walk runs off the UI thread. The real source with a token
    cancelled before the call throws `OperationCanceledException` at once.

Relative folder and rename:
12. `FileListText.RelativeFolder(rootFolder, item)` returns `""` for an item of the root and the path under the root for a
    deeper one (`sub\deep`), ignoring case and trailing separators (`C:\Pics`, `c:\pics\a\b` gives `a\b`); an item outside
    the root returns its full folder path.
13. Given a batch with files from two folders of a real temp tree, then each is renamed in its own folder through the usual
    journal (the entry lists both folders), and Revert restores both. `a.txt` in `x` and `a.txt` in `y` both renamed to
    `b.txt` is not a conflict; two `b.txt` in one folder are.

## Out of scope
- The controls and the Folder column (NS-70); following junctions; a listing cap.
- A folder and a file inside it in the same batch: the executor orders the moves (NS-8) but `PathLengthCheck` measures only
  each item's own new path, so a child path that becomes too long after its parent is renamed is not predicted. Accepted
  as in NS-7 ("folder only for now"); the rename call then fails and the batch is rolled back, never half done.

## Decisions
- Cycles through junctions? **Decision:** reparse points are never followed (po, 2026-10-18).
- Order with sub-folders? **Decision:** grouped by folder, root first, so numbering (and NS-60's restart per folder) sees each
  folder's files together (po, 2026-10-24).
- Unreadable sub-folder? **Decision:** skip it and say how many; the duplicate check still sees every name of every folder
  read (po, 2026-10-24).
- Listing cap? **Decision:** none; cancel and a responsive window are the safety (po, 2026-10-24).
- Is the option kept when I change folder? **Decision:** yes during the run, off at start and on Reset (po, 2026-10-24).

## Log
- 2026-10-24: split out of NS-33 after the PR #53 review (seam, cancellation contract, segment order, no false claim about
  long child paths); Draft until NS-33 is merged (po).
