# NS-70: Filter row above the file list

Status: Draft
Depends on: NS-33, NS-74
Area: app

## Why
The window controls for the filters and the sub-folders option (part b of the original NS-33). Set Ready when NS-33 and
NS-74 are merged.

## Acceptance criteria
Layout decided by David (2026-10-24): one compact row above the file list: a mask box, Files and Folders check boxes,
Hidden, Sub-folders, and a "More" part for the regex and the lengths. A `UserControl` in `src/NameShift.App/Controls/`
docked into a named host panel; it only renders and forwards input to `FilterPanelModel` (NS-33); sizes in 96-DPI pixels.
Logic that can be tested goes to `NameShift.Ui` (text and mnemonics in `Strings`).
1. Given the window, then the row is above the file list, one line high at 100 %: label `Mask:` and a text box, check boxes
   `Files`, `Folders`, `Hidden`, `Sub-folders`, and a `More` toggle at the right. Files and Folders start checked, the others
   not.
2. Given I click `More` (or press its Alt key), then a second line appears with `Regex:` and its text box, `Ignore case`, and
   `Min length:` and `Max length:` boxes; clicking again hides it. The values stay when hidden and still apply; the toggle
   text shows when a hidden value is active (`More *`, or the accessible description says so).
3. Given I type in the mask or regex box, then the list updates after 250 ms without typing (one update, not one per key);
   a check box updates it at once. By hand, plus a Ui test of the debounce rule if it lives in a model.
4. Given an invalid regex or length, then the model's `Error` text shows under the row in the conflict colour with the
   non-colour cue of the text itself, and the list keeps filtering with the bad part ignored (NS-32).
5. Given the Sub-folders box is ticked, then the list adds a `Folder` column with `FileListText.RelativeFolder` (empty
   for the root), the status bar says how many folders could not be read when `SkippedFolders` is above 0, and unticking
   removes the column.
6. Given the Reset button (NS-15), then it also resets the filter row (NS-33's `Reset`: everything default, Sub-folders off,
   `More` hidden); the folder and the selection stay. The Reset button is enabled when a panel or the filters are not at
   default.
7. Given the list is filtered, then the "N of M files" line counts the shown items (M = shown), and the line says
   `filtered` when something is hidden by the filter (new string, a Ui test of the text).
8. Alt keys: each label has its own mnemonic letter, different from every rule panel's and the buttons'; a Ui test lists every
   mnemonic string of the window (panels, buttons, filter row) and fails on a duplicate letter.
9. Given the Tab order, then it goes path bar, tree, filter row, file list, rule panels, buttons; each control has an
   accessible name from `Strings`. Works at 150 % and 200 % with the row not clipped (by hand).

## Out of scope
- Saving filters in presets (NS-36 may add them later) and remembering them (NS-37).
- A pop-up dialog for filters.

## Decisions
- Placement? **Decided by David (2026-10-24):** one compact row above the file list, as above.
- Reset? **Decision:** one Reset for everything the user typed, as NS-37's `Reset all` says (po, 2026-10-24).

## Log
- 2026-10-24: created from the original NS-33 (po); Draft until NS-33 (the logic) is merged.
