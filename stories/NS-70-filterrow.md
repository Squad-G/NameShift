# NS-70: Filter row above the file list

Status: In Review
Depends on: NS-33
Area: app

## Why
Let the user filter the list from the window: by mask, kind, hidden files, regex and name length. (Part b of the original
NS-33; the Sub-folders box and the Folder column are NS-78.)

## Acceptance criteria
Layout decided by David (2026-10-24): one compact row above the file list: a mask box, Files and Folders check boxes,
Hidden, and a "More" part for the regex and the lengths (the Sub-folders box joins the row in NS-78). `FilterRow` is a
`UserControl` in `src/NameShift.App/Controls/`, docked into a new named host panel `FilterHost` of `MainForm`, in the
right split above `FileListHost`; it only renders and forwards input; sizes in 96-DPI logical pixels. Logic that can be
tested goes to `NameShift.Ui` with tests on net10.0 and net48 (text and mnemonics in `Strings`). Starting point (merged
NS-33): `FilterPanelModel` (`Changed`, `Reset`, `IsDefault`, `Filter`, `Error`), `FileListModel.SetFilterAsync`,
`IsFilterDefault`, `Message`, `OrderChanged`, `MainActions`, `FileList.RenderOrder`.

Row:
1. Given the window, then the row is above the file list, one line high at 100 %: label `Mask:` and a text box, check boxes
   `Files`, `Folders` and `Hidden`, and a `More` toggle at the right. Files and Folders start checked, Hidden not. The row
   sits in the split `OnActionsChanged` already disables, so it is disabled while a Rename or Revert runs.
2. Given I click `More` (or press its Alt key), then a second line appears with `Regex:` and its text box, `Ignore case`,
   `Min length:` and `Max length:` boxes; clicking again hides it. The values stay when hidden and still apply. When the
   line is hidden and any of its four values is not at default, the toggle's accessible description says `Regex or length
   filter active` and its text shows `More *` (the star is not the only cue). A Ui test of the rule (`FilterRowText`): all
   defaults gives no mark; a pattern, ignore case, a minimum or a maximum each gives it.
3. Given I type in the mask, regex or length boxes, then the model is updated 250 ms after the last key (one update for a
   burst, not one per key); a check box updates it at once. A Ui class (`FilterTyping`, indicative) holds the rule, with an
   injected delay as `PreviewModel` has: `Type(field, text)` restarts the wait; when it ends, the pending texts are set on
   the `FilterPanelModel`; `Flush()` sets them at once (called before a check box change, on Enter and when a box loses
   focus, so the order the user made is kept); `Dispose()` drops what is pending. Tests with a fake delay: two keys in the
   wait give one set with the last text; `Flush` sets at once and nothing happens when the wait ends; a check box change with
   text pending sets the text first; `Dispose` sets nothing. Applying several pending texts may raise several `Changed`;
   the last filter wins (the file list supersedes a pending run).
4. Given the model's `Error` is not null (invalid regex, a length that is not a whole number, a minimum above the maximum),
   then its text shows under the row in the conflict colour (`Palette.ConflictText`, bold in a high-contrast theme as the
   conflict text is elsewhere: the words themselves are the cue), the label has an accessible name, and the list keeps
   filtering with the bad part ignored (NS-32). It is hidden when `Error` is null.

Wiring:
5. `FilterBinding(filterModel, fileList)` (Ui) subscribes to `FilterPanelModel.Changed` and calls
   `FileListModel.SetFilterAsync(filterModel.Filter)` once per `Changed` (fire and forget, exceptions swallowed: the
   model never throws). Tests with a fake source: a mask change filters `Items`; a `Reset()` raises one `Changed` and gives
   one run; changing a check box and back leaves the list equal to the unfiltered one. The window creates the model and the
   binding once; the filters stay when another folder opens (the model applies the requested filter to every load), start at
   default, and are not saved (NS-37 does not remember them).
6. Given the Reset button (NS-15), then it resets the rules and the filters (`FilterPanelModel.Reset`: every value at its
   default; the row's `More` toggle keeps its state, since it is view state the model does not know, so the values clear and
   the line stays as the user left it); the folder, the sort and the selection stay (the selected items still listed stay selected, as
   a filter change does). `MainActions` takes the `FilterPanelModel`: `CanReset` is true when a panel has something to reset
   or the filters are not at default (and not busy); `Changed` is raised when the filters change; `Reset()` does both; the
   documentation of `Reset` ("never touches files, journal or selection") says what it does now. Tests in `MainActions`'
   tests: a filter set enables Reset; Reset clears both; busy keeps it off. The `MainActions` constructor change updates
   its tests and `MainForm`.

List:
7. Given the list is filtered, then the count line `N of M files` counts the shown items (M is `Items.Count`) and ends with
   ` (filtered)` when the filter is not at default (`!IsFilterDefault`) and hides at least one entry
   (`Items.Count < AllItems.Count`); the default filter alone, which hides hidden files, never adds it, so nothing changes for
   a user who never touches the filter. New strings `SelectionCountFiltered` (the count text plus ` (filtered)`); a Ui test of
   the text through `FileListText.SelectionCount` with the new flag (filtered or not, one file, many files).
8. Given a filter changes the number of rows (`OrderChanged`), then `FileList.RenderOrder` follows it: it sets
   `VirtualListSize` to `Items.Count` before it selects any row, keeps `_shownCount` and `_shownItems` current, shows or
   hides the empty-message label from `FileListModel.Message` (`No item matches the filters.`), redraws the count and the
   conflict line, restores the model's selected rows (none when the list is empty, nothing indexes `Items[rows[0]]` past
   the end), and scrolls to the first selected row, or to the top when the list shrank and nothing is selected. Today it
   assumes the count is unchanged. By hand, written in the PR: filter to zero rows and back; filter with some rows selected
   (those still listed stay selected); filter while a selection read is pending (`ReadSelection` calls `SetSelection`, which
   throws for a row outside `Items`: a read scheduled before the shrink must not index past the end); filter while scrolled to the bottom of a long list; sort after a filter; no exception
   in any of them.
9. Given the window closes, then `MainForm` disposes `FileListModel` (a `Dispose(bool)` override) and the row's
   `FilterTyping`, so a load or filter run still in flight never raises an event on a dead window. (`FileListModel.Dispose`
   exists and is tested; today nobody calls it.)

Alt keys and access:
10. The first line of the row has its own Alt keys: `Mas&k:` (K), `Fi&les` (L), `Folder&s` (S), `&Hidden` (H) and
    `M&ore` (O); `More *` keeps the letter O. The second line has no Alt keys: Alt+O opens it and moves the focus to the
    `Regex:` box, and Tab moves on from there. Letters are unique among the rule panel titles (R, C, M, X, E, G, V, D, F,
    A, N), the menu bar's (P, NS-72), and the row's own; B is reserved for NS-78's Sub-folders box (lead, 2026-10-24).
    `WindowMnemonicTests` (created by NS-72 or by this story, whichever merges first) lists the row's first-line strings
    and fails on a repeated letter among titles, menu and row; it also checks that `More *` keeps the letter of `More`,
    that the row never uses B and that the second line's texts have no Alt key. Letters shared between a label of one
    panel and another panel stay allowed, as today.
11. Given the Tab order, then it goes path bar, folder tree, filter row, file list, rule panels, buttons; each control has an
    accessible name from `Strings`. At 150 % and 200 % the row is not clipped and `More` stays reachable (by hand).

## Out of scope
- The Sub-folders box and the Folder column (NS-78); saving filters in presets and remembering them between runs (NS-36 and
  NS-37 do not).
- A pop-up dialog for filters; size or date filters (NS-32).

## Decisions
- Placement? **Decided by David (2026-10-24):** one compact row above the file list, as above.
- Reset? **Decision:** one Reset for everything the user typed (rules and filters), as NS-37 says; no `Reset all` menu item.
  It clears the values and leaves the `More` line as it is (view state, not model state) (po, 2026-10-24).
- Does this wait for sub-folders? **Decision:** no: the filters are useful alone, so the Sub-folders box and the Folder column
  move to NS-78, which needs this row and NS-74; the row's layout is unchanged for David's decision (po, 2026-10-24).
- Do the filters survive a folder change? **Decision:** yes, as the model already does (the requested filter applies to every
  load) and the row shows what applies, so nothing is hidden unseen (po, 2026-10-24).
- Typing delay? **Decision:** 250 ms, a little more than the 150 ms of the rule preview since each update reruns the preview
  too; check boxes update at once (po, 2026-10-24).
- Alt keys of the row? **Decision (option B, lead, 2026-10-24):** the first line has its own letters (K, L, S, H, O); the
  More line has none, and Alt+O opens it and focuses the `Regex:` box. `Regex:` has no free letter, and with More on O
  and Mask on K the six other texts compete for five letters (H, I, L, S, T), so not every text can have one.

## Log
- 2026-10-24: created from the original NS-33 (po); Draft until NS-33 (the logic) is merged.
- 2026-10-24: NS-33 merged. Split again: the sub-folder parts moved to NS-78 so this story no longer waits for NS-74.
  Added the `FileList.RenderOrder` row-count fix, `MainForm` disposing `FileListModel`, the typing debounce class, the
  `MainActions` filter reset and the filtered count rule; set Ready (po).
- 2026-10-24: In Progress (builder).
- 2026-10-24: AC 10 reworded to the decided Alt keys (option B: no Alt keys on the More line) (builder).
- 2026-10-24: In Review (builder), PR #80.
