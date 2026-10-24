# NS-78: Sub-folders box and Folder column

Status: Draft
Depends on: NS-70, NS-74
Area: app

## Why
Tick one box to list and rename the files of all sub-folders, and see which folder each file is in. Draft only until NS-70
(the filter row) and NS-74 (the listing) are merged; its criteria are complete, so it becomes Ready when both are.

## Acceptance criteria
`FilterRow` (NS-70) and `FileList` change; logic in `NameShift.Ui` with tests on net10.0 and net48; text and mnemonics in
`Strings`. Names are indicative.
1. Given the row, then a check box `Sub-folders` (own Alt key, from the free letters of NS-70:10) sits after `Hidden`,
   unticked at start; ticking it sets `FilterPanelModel.IncludeSubfolders` (NS-74:4) at once (no typing delay).
2. Given `FilterPanelModel.IncludeSubfolders` changes (the box, or Reset), then `FilterBinding` (NS-70:5) also calls
   `FileListModel.SetIncludeSubfoldersAsync(model.IncludeSubfolders)` (NS-74:5) after it has asked for the filter; a Reset
   that changes only the box gives one reload and no filter run (the filter is the same instance, NS-74:4). A Ui test with a
   fake source: tick, the source is called with `includeSubfolders` true and a token; untick, called with false; the
   selection is cleared by the reload; Reset with the box on reloads once.
3. Given the Sub-folders box is ticked, then the list adds a `Folder` column after `Name` (header `Folder`) showing
   `FileListText.RelativeFolder(model.FolderPath, item)` (NS-74:14: empty for the root); unticking removes it. The column
   index constants of `FileList` stay correct for the sort and the arrow (the Folder column is not sortable: a click does
   nothing). The column shows once the box is ticked, even while the folder loads.
4. Given a listing that skipped folders (`FileListModel.SkippedFolders` above 0), then the status bar says `N folders could
   not be read` (one: `1 folder could not be read`) when the load lands, once per load; none says nothing. A Ui test of the
   text (`FileListText` or the status text class, one and many).
5. Given the box is ticked on a large tree, then the window stays responsive while it loads (`Loading...` shows, the folder
   tree and the path bar still work) and opening another folder or unticking the box cancels the walk (NS-74:13) (by hand,
   on a tree of a few thousand folders, written in the PR).
6. Given `MainForm` already handles `FileListModel.Changed`, then a selection requested from the command line or a drop
   (`FindRow`, NS-47) still selects a file of the root folder when the box is ticked, and finds nothing for a file of a
   sub-folder (NS-74:12).
7. Given the Numbering panel's `Restart in each folder` (NS-71) is ticked and the box is ticked, then the preview numbers
   each folder from the start again (by hand, with two sub-folders; the engine case is tested in NS-60).
8. Given the Tab order and Alt keys, then the box takes its place in NS-70's order and `WindowMnemonicTests` has the new
   string; the box is disabled with the rest of the split while a Rename or Revert runs.

## Out of scope
- The listing itself (NS-74); filters; the command line (NS-41).

## Decisions
- Is the Folder column sortable? **Decision:** no: the groups stay in tree order (NS-74:11) and a sort by folder would only
  reorder the groups, which the tree order already gives (po, 2026-10-24).
- Where does the Folder column go? **Decision:** right after Name, since it explains the name's place; shown only when the
  box is on, so the default window is unchanged (po, 2026-10-24).

## Log
- 2026-10-24: created when NS-70 was split (po); Draft until NS-70 and NS-74 are merged.
