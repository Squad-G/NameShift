# NS-33: Filters and sub-folders: model and listing

Status: Ready
Depends on: NS-32, NS-51, NS-66
Area: app

## Why
Filter the list and include the sub-folders, in the window's logic (`NameShift.Ui`). The controls are NS-70. (Part a of
the original NS-33; the numbering check box is NS-71.)

## Acceptance criteria
Names are indicative; the tech lead picks them. All code is in `NameShift.Ui` and `NameShift.Core` (no WinForms); tests on
net10.0 and net48; every new visible text is in `Strings.resx`.

Filter settings (`FilterPanelModel`, plain properties, `Changed`, `Reset()`, `IsDefault`, as the rule panels' models):
1. Given a new model, then Mask and Pattern are empty, IgnoreCase false, IncludeFiles true, IncludeFolders true,
   IncludeHidden false, MinLength and MaxLength texts empty, IncludeSubfolders false, `IsDefault` is true, and `Filter` is an
   `ItemFilter` equal to `new ItemFilter()`.
2. Given any property is set to a different value, then `Changed` is raised once and `IsDefault` is false; set to the same
   value, nothing is raised; a null text counts as empty; `Reset()` restores every default and raises `Changed` once (not at
   all when already default).
3. Given MinLength text `5` and MaxLength text `8`, then `Filter.MinLength` is 5 and `Filter.MaxLength` is 8. Given text
   that is not a whole number from 0 up (`abc`, `-1`, `1.5`, `99999999999`), then that limit is ignored and `Error` says
   `Enter a whole number for the length.` (new string); the text is kept as typed.
4. Given an invalid regex pattern or a minimum above the maximum, then `Error` is the filter's own text (NS-32:7, 12); the
   model adds nothing of its own. With no problem `Error` is null. Several problems are joined by one space.
5. Given the filter changes, then `Filter` is a new, already built `ItemFilter` (patterns are not rebuilt per item).

File list (`FileListModel`):
6. Given a loaded folder, then `AllItems` holds every entry the listing returned (hidden and system ones too) in list order
   and `Items` holds `Filter.Apply(AllItems)`: with the default filter, `Items` equals what the window shows today (no hidden or
   system item). `DiskFileListSource.IsShown` is removed: the source returns hidden and system entries with their
   attributes, and the existing tests are updated.
7. Given a filter change (not Sub-folders), then `Items` is a new list, `Batch` is rebuilt, the selected items that are still
   listed stay selected (by item, as a sort does, NS-66), the source is not called again, and the same single event as a new
   order (NS-66's `OrderChanged`) is raised once, so the preview recomputes once.
8. Given the filter hides every item of a non-empty folder, then `Message` is `No item matches the filters.` (new string);
   an empty folder still says `This folder is empty`.
9. Given `ExistingNamesByFolder` (replaces `ExistingNames`; a map from folder path to every entry name of that folder, hidden
   and system included, never filtered), then `PreviewModel` uses it for the duplicate check: a file hidden by the filter
   still occupies its name (`a.png` filtered out, `a.jpg` renamed to `a.png` is flagged `TargetExists`, NS-32:15).
10. Given a 10,000-item list and a mask change, then re-filtering finishes in under 1 second (a loose guard, as NS-58).

Sub-folders (the source and the load):
11. `IFileListSource.ListEntries(folder, includeSubfolders, cancellationToken)`. Given IncludeSubfolders false, then the
    listing is the folder alone, as today.
12. Given IncludeSubfolders true and a temp tree `root\a.txt`, `root\x\b.txt`, `root\x\y\c.txt`, then every file and every
    folder of the tree is listed, each with its own `FolderPath`, and `ExistingNamesByFolder` has one entry per folder read.
13. Given a folder that is a reparse point (a junction made with `mklink /J` in the test; the test is skipped with a message
    when it cannot be made), then it is listed as a folder item but never entered, so a loop (`root\loop` pointing to
    `root`) ends. Symbolic links to folders are the same.
14. Given a sub-folder that cannot be read (access denied, a path too long for the system, removed meanwhile), then it is
    skipped, the rest is listed, and the listing's `SkippedFolders` count says how many (shown by NS-70). The root folder
    failing is the usual folder problem.
15. Given a tree 200 folders deep, then it is listed without a stack overflow (the walk uses a queue or stack of its own).
16. Order: items are grouped by folder, the root folder first and then each sub-folder in the natural order of its path, a
    parent before its children; inside a group the order and the NS-66 sort are those of one folder (folders first, then
    the column's order). So `root\x\b.txt` comes after every item of `root` and before `root\y\a.txt`. With
    IncludeSubfolders false the order is unchanged.
17. Given the property IncludeSubfolders changes, then the current folder is loaded again with the new value (state
    `Loading`, then `Loaded`), the selection is cleared as for any load, and the sort and the other filter values are kept;
    `FindRow(name)` (NS-47) still finds a root-folder item only.
18. Cancel: a new load, a change of IncludeSubfolders, or `Cancel()` cancels the running listing: the source receives a
    cancelled token, it checks it at least once per folder and once per 1,000 entries, the model treats the resulting
    `OperationCanceledException` as a superseded load (returns false, no `Failed` state and no message), and the window
    stays responsive because the walk runs off the UI thread. Tests: a fake source that blocks until its token is
    cancelled; and the real source with a token cancelled before the call returns promptly.
19. Given IncludeSubfolders and the app starting or `Reset()`, then it is off (criteria 1, 2).

Relative folder:
20. `FileListText.RelativeFolder(rootFolder, item)` returns `""` for an item of the root and the path under the root for a
    deeper one (`sub\deep`), comparing ignoring case and trailing separators (`C:\Pics`, `c:\pics\a\b` gives `a\b`); an item
    outside the root returns its full folder path.

Rename in place:
21. Given a batch with files from two folders of a tree, then the renamer renames each in its own folder through the usual
    journal (the journal entry lists both folders), and `a.txt` in `x` and `a.txt` in `y` both renamed to `b.txt` is not a
    conflict, while two `b.txt` in the same folder are.
22. Given a folder item and a file inside it in the same batch, then the batch runs (the executor handles parent folders, NS-8)
    and Revert restores both; the preview's path-length check uses the original parent path, so a child that would become too
    long after its parent is renamed is refused by the executor and rolled back, never partly renamed.

By hand (listed in the PR):
23. A folder with 100,000 files in 2,000 sub-folders: the window stays responsive while listing, changing folder or toggling
    cancels the first listing, and memory stays reasonable.

## Out of scope
- The controls (NS-70) and the numbering check box (NS-71, engine NS-60).
- Following junctions or links, a listing cap, a size or date filter, a "not" mask (NS-32).
- A relative-folder column or a tree view of the result (NS-70 shows the folder).

## Decisions
- Cycles through junctions? **Decision:** reparse points (junctions, links) are never followed, to avoid loops and surprises
  (po, 2026-10-18).
- Hidden files? **Decision:** the listing then returns hidden and system entries too (with their attributes) and `ItemFilter`
  (NS-32) leaves them out by default, so "Hidden" is one switch in the filter, not a second listing; `ExistingNames` keeps
  every entry, now per folder (po, 2026-10-24).
- Where does the filter control sit? **Decided by David (2026-10-24):** the default: one compact row above the file list
  (NS-70). Split: this story is the logic (a), NS-70 the controls (b), NS-71 the numbering check box (po, 2026-10-24).
- Order with sub-folders? **Decision:** grouped by folder, root first, so numbering (and NS-60's restart per folder) sees each
  folder's files together and the list reads like a tree (po, 2026-10-24).
- Unreadable sub-folder? **Decision:** skip it and say how many, never fail the whole listing: the user can still rename what
  was read, and the duplicate check still sees every name of every folder read (po, 2026-10-24).
- Listing cap? **Decision:** none; cancel and a responsive window are the safety (po, 2026-10-24).
- Is the sub-folders option kept when I change folder? **Decision:** yes during the run, off at start and on Reset
  (criterion 19, the original NS-33:4) (po, 2026-10-24).

## Log
- 2026-10-18: created (po).
- 2026-10-24: placement question answered by David (default); split into model (this story), controls (NS-70) and the
  numbering check box (NS-71); criteria made testable; set Ready (po).
