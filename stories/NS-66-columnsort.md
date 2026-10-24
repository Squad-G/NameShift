# NS-66: Column sorting

Status: Ready
Depends on: NS-51, NS-14, NS-13
Area: app

## Why
Order the list by name, size or date, and number the files in that order (the list order is the pipeline order, NS-17).
(Split out of NS-46.)

## Acceptance criteria
Logic is in `NameShift.Ui` (`FileListModel`, `FileListOrder`); the header click and arrow are in `FileList`.
Names below are indicative: the tech lead may pick others.

Order (`FileListOrder.Sort(items, sort)`, with `sort` = a column Name, Size or Modified and a direction):
1. Given Name ascending, then the order is today's: folders first, then files, each in natural name order
   (`a2.txt` before `a10.txt`), ties broken by ordinal name. Name descending is the exact reverse inside each group.
2. Given Size ascending, then files are ordered by `RenameItem.Size` (`9 B`, `10 B`, `2 KB`, numerically, not as text); items
   with the same size are in natural name order, ascending. Size descending reverses the size order only: ties stay in
   natural name order ascending.
3. Given Modified ascending, then items are ordered by `RenameItem.Modified` (oldest first), ties in natural name order
   ascending; descending is newest first, ties still ascending by name. An item whose date is unknown (`default`) counts as the
   oldest.
4. Given any column and either direction, then folders stay before files (never mixed). Folders are ordered inside their
   group by the same column (a folder's Size is 0 for every folder, so they fall back to the name).
5. Given a folder of 10,000 items, then a sort completes in under 1 second on the pool (a loose guard, as in NS-58).

Model (`FileListModel`):
6. Given a loaded list, when `SortBy(column)` is called, then: a column that is not the current one sorts ascending; the
   current column toggles ascending and descending (there is no "unsorted" third click); `Items` is the new order and a new
   list instance; `ExistingNames` and `FolderPath` are unchanged.
7. Given rows 0 and 2 are selected, when the list is sorted, then the same two items stay selected (by item, not by row
   index): `Batch.Items` holds those items in the new list order and the selected rows are their new row indexes. With
   nothing selected (or everything selected) the batch is still the whole list, now in the new order.
8. Given a sort, then `Batch` is rebuilt for the new `Items` so `PreviewModel.TryGetRow` stays consistent, `Changed` and
   `SelectionChanged` are not raised, and one new event (`OrderChanged`) is raised once. A test with a counting
   `PreviewModel` shows exactly one new preview run (no debounce) per sort, not one per event.
9. Given the model exposes the selected rows (a `SelectedRows` list: empty for none, every row for all), then after a sort it
   holds the new row indexes of the same items, so the control can re-select them (the whole-list batch alone cannot tell
   "none" from "all").
10. Given Numbering (NS-17) enabled and Size descending, then the preview numbers the files in the list order shown (the
    biggest file gets the start number).
11. Given a different folder is loaded, then the sort goes back to the default (Name ascending, no arrow). Given the same
    folder reloads (after Rename, Revert or a refresh, compared with `PathInput.SameFolder`), then the sort is kept, so a
    batch numbered by date keeps its order on screen.
12. Given the list is `Loading`, `Failed` or empty, then `SortBy` does nothing and raises nothing.

Window (by hand, listed in the PR):
13. Clicking the Name, Size or Modified header sorts as above; New Name and Status headers do nothing when clicked.
14. The sorted column's header shows a small up (ascending) or down (descending) arrow, drawn with the header's own sort
    flag (no text change, so the column names in `Strings` stay as they are); a freshly loaded folder shows no arrow.
15. After a sort, the previously selected rows are selected again (and the first of them scrolled into view), the "N of M
    files" count is unchanged, Rename stays disabled until the new preview is applied and then enables as before.
16. Works at 150 % and 200 % scale and with a Windows contrast theme (the arrow comes from the system header).

## Out of scope
- Sorting by the New Name column (it changes as the rules change, which would reorder rows while typing).
- Remembering the order between runs (NS-37).
- A keyboard-only way to sort (the header is mouse only for now); to decide with NS-54, noted as a gap.
- Sorting folders-and-files together.

## Decisions
- Folders first? **Decision:** yes in every order, as in Explorer's default, so numbering never mixes folders into files
  by surprise (po, 2026-10-24).
- First click direction? **Decision:** ascending for every column (smallest, oldest, A first), a second click reverses; the
  same rule for all three is simpler to learn than Explorer's per-column defaults (po, 2026-10-24).
- Ties? **Decision:** always natural name ascending, even in a descending order, so equal items never jump around (po,
  2026-10-24).
- Keep the sort after Rename? **Decision:** yes for the same folder, reset for another folder; this changes the earlier
  "a folder load goes back to default" so a user who numbered by date sees the result in that order (po, 2026-10-24).
- Selection by item, not row: the model keeps the selected items itself (`SelectedRows`) because a whole-list batch means
  both "none" and "all" selected (po, 2026-10-24).

## Log
- 2026-10-24: split out of NS-46 (po).
- 2026-10-24: criteria made testable; set Ready (po).
