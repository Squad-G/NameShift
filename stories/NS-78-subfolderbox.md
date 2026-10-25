# NS-78: Sub-folders box and Folder column

Status: Done
Depends on: NS-70, NS-74
Area: app
Build after NS-70 is merged (it extends NS-70's `FilterRow`, `FilterBinding`, `FilterTyping` use and `WindowMnemonicTests`). NS-74 is merged.

## Why
Tick one box to list and rename the files of all sub-folders, and see which folder each file is in.

## Acceptance criteria
`FilterRow` (NS-70) and `FileList` change; logic in `NameShift.Ui` with tests on net10.0 and net48; text and mnemonics in
`Strings`. Starting point, merged: `FilterPanelModel.IncludeSubfolders` (NS-74:4: one `Changed`, same `Filter` instance, in
`IsDefault` and `Reset`), `FileListModel.IncludeSubfolders` and `SetIncludeSubfoldersAsync` (NS-74:5), `SkippedFolders`
(NS-74:3), `FileListText.RelativeFolder` (NS-74:14), `FileListModel.FindRow` (NS-74:12). Names are indicative.
1. Given the row, then a check box `Sub-folders` sits at the end of the first line, after `Hidden` and before the More
   toggle, unticked at start, with the Alt key `Su&b-folders` (B). The letters in use are the panel titles and the menu (R C
   M X E G V D F A N P) and NS-70's first line (K L S H O): B is free among these top-level texts (inside the Remove panel `Crop &before` also uses B, which the
   existing tests allow: a panel's own letters are separate), and so is U. NS-70's list of free letters still contains B:
   it is reserved for this box, and NS-70 must not give it to the More line (the NS-70 brief is updated for this). `WindowMnemonicTests.TitleLetters_Distinct`
   gets the new string `FilterSubfolders` (and fails if the letter repeats); its accessible name is the text without `&`.
   Ticking sets `FilterPanelModel.IncludeSubfolders` at once: `FilterTyping.Flush()` first (so a mask being typed applies
   before, as for every check box in NS-70), then the model, with no typing delay.
2. Given `FilterPanelModel.IncludeSubfolders` changes (the box, Reset, or a code path), then `FilterBinding` (NS-70) also calls
   `FileListModel.SetIncludeSubfoldersAsync(model.IncludeSubfolders)` after it has asked for the filter, and keeps the task
   in an internal `LastReload` for tests. A change of only the box gives one reload and no filter run (the `Filter` is the
   same instance, so `SetFilterAsync` does nothing). A Reset that changes the box and a filter value ends consistent: the
   list holds the unfiltered, folder-only listing, whichever of the two runs the reload supersedes (a test awaits both
   tasks). A Ui test with a fake source: tick, the source is called with `includeSubfolders` true and a token; untick, called
   with false; the selection is cleared by the reload; Reset with the box on reloads once; ticking before any folder is shown
   stores the value and the first load uses it; a disposed binding does nothing.
3. Given the box is ticked, then the list adds a `Folder` column right after `Name` (new string `ColumnFolder` = `Folder`)
   showing `FileListText.RelativeFolder(model.FolderPath, item)` (empty for the root); unticking removes it, and a user's
   widths of the other columns are kept. The column indices are not hard-coded: a new `NameShift.Ui` class
   (`FileListColumns`) maps the logical columns (Name, Folder, New name, Status, Size, Modified) to the indices of the
   shown list for a `showFolder` flag, and `FileList` reads every index (draw, sort click, sort arrow, widths) from it. Tests:
   the order for both flags, the index of each column, a click on the Folder column maps to no sort, and the arrow of the
   current sort sits on the right header with the column shown and hidden. The Folder column is not sortable: a click does
   nothing (the groups stay in tree order, NS-74:11). The column shows as soon as the box is ticked, even while the folder
   loads (it is bound to the box, not to the listing), and its width is `LogicalToDeviceUnits(160)`.
4. Given a listing that skipped folders (`FileListModel.SkippedFolders` above 0), then the status bar (`MainForm.ShowStatus`)
   says `N folders could not be read` (one: `1 folder could not be read`) when the load lands, once per load; none says
   nothing; a reload that lands without skips does not repeat it. A `NameShift.Ui` class (a notice that watches
   `FileListModel.Changed` and the `Loading` to `Loaded` step) returns the text or null, with the one and many forms in
   `Strings` (`SkippedFolders`, `SkippedFolder`, `{0}` formatted with `N0` in the current culture), tested with a fake source
   that skips 0, 1 and 3 folders, a second load with 0, and a load superseded before it lands (no text).
5. Given the box is ticked on a large tree, then the window stays responsive while it loads (`Loading…` shows, the folder tree
   and the path bar still work) and opening another folder or unticking the box cancels the walk (NS-74:13) (by hand, on a
   tree of a few thousand folders, written in the PR). Closing the window while a walk runs ends cleanly (NS-70's disposal
   order disposes the model, which cancels the walk).
6. Given `MainForm` already handles `FileListModel.Changed`, then a selection requested from the command line or a drop
   (`FindRow`, NS-47) still selects a file of the root folder when the box is ticked, and finds nothing for a file of a
   sub-folder (NS-74:12): a Ui test with the fake source over a tree and the box on (the existing `FindRow` tests of NS-74
   stay as they are).
7. Given the Numbering panel's `Restart in each folder` (NS-71) is ticked and the box is ticked, then the preview numbers each
   folder from the start again (by hand, with two sub-folders; the engine case is tested in NS-60), and a Rename of files of
   two folders refreshes the folder tree nodes of both (NS-74:16: by hand).
8. Given the Tab order and Alt keys, then the box takes its place after `Hidden` in NS-70's order; the box is disabled with the
   rest of the split while a Rename or Revert runs (it sits inside `_mainSplit`, so no extra code: checked by hand); and
   `Reset` unticks it (the row's `Sync` sets it from the model).
9. Given the filters are not saved (NS-70, NS-37), then the box is not saved either: the window starts with it off every time,
   and a preset never carries it (a test that `PresetStore` and `Preset` have no field for it is not needed: the existing
   tests of NS-36 pin the saved settings).

## Out of scope
- The listing itself (NS-74); the filters (NS-70); the command line (NS-41); a Folder column sort; showing the box in the
  More line.

## Decisions
- Is the Folder column sortable? **Decision:** no: the groups stay in tree order (NS-74:11) and a sort by folder would only
  reorder the groups, which the tree order already gives (po, 2026-10-24).
- Where does the Folder column go? **Decision:** right after Name, since it explains the name's place; shown only when the
  box is on, so the default window is unchanged (po, 2026-10-24).
- Which Alt key? **Decision:** B (`Su&b-folders`): the other letters of the word are taken by the panel titles, the menu and
  NS-70's first line, and the box is on the first line with the other check boxes, not behind More (po, 2026-10-24).
- Who tells the user about unreadable folders? **Decision:** one status-bar line per load (routine feedback, no
  `MessageBox`), from a tested Ui class: a count the user can act on, and no list of folders (po, 2026-10-24).
- Column indices? **Decision:** a tested `FileListColumns` map instead of constants that shift: the sort click and the arrow
  are the usual place for an off-by-one when a column appears (po, 2026-10-24).
- Box and filter in one `Changed`? **Decision:** the binding asks for the filter first and the reload second; the reload reads
  the filter asked for last, so the end state does not depend on which run wins (po, 2026-10-24).

## Log
- 2026-10-24: created when NS-70 was split (po); Draft until NS-70 and NS-74 are merged.
- 2026-10-24: PR #75 review applied: B is free among top-level texts only, and reserved for this box in NS-70's list (po).
- 2026-10-24: NS-74 merged. Re-checked against its API and the NS-70 brief (first-line Alt keys K L S H O, FilterBinding,
  FilterTyping): Alt key B chosen, the column map and the skipped-folders notice moved to tested `Ui` classes, the Reset
  case and the disposal added; set Ready, to start after NS-70 is merged (po).
- 2026-10-24: In Progress (builder).
- 2026-10-24: In Review (builder), PR #81.
- 2026-10-24: Done (tech-lead), PR #81 approved on re-review and squash-merged. Hand checks for David (PR #81 checklist):
  AC 1 and 8 (the box after Hidden, unticked at start, Alt+B, Tab Hidden > Sub-folders > More, disabled while busy,
  Reset unticks it); AC 3 (the Folder column with the box, also while loading; widths kept across a hide; a click on
  Folder does nothing; the sort arrow on the right header); AC 4 (the skipped-folders line once per landed load, none
  repeated, unticking clears that line but not a newer message); AC 5 (a large tree stays responsive, cancelling and
  closing during a walk); AC 7 (Restart in each folder per sub-folder; a Rename across two folders refreshes both
  nodes); AC 9 (off at every start, presets never change it); 150 % and 200 % at the minimum width, More reachable.
  Follow-up nits: mutation 5 is guarded indirectly (say which test catches it); the notice tests record into
  unlocked lists; in a fresh window the first change runs a filter that the reload then cancels (from NS-70).
  One unnamed net48 Ui failure in the first merge check run, not reproduced in 40 re-runs; tracked by NS-77.
