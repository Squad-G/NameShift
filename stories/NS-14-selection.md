# NS-14: Choose which files to rename

Status: In Review
Depends on: NS-51, NS-13
Area: app

## Why
Rename only the files I pick, not always the whole folder.

## Acceptance criteria
1. Given no selection, then every listed file is included in the batch.
2. Given I select some rows (click, Shift, Ctrl, Ctrl+A), then only those are renamed and the others show no New Name.
3. Given an unselected file would collide with a new name, then the collision is still detected (`TargetExists`).
4. Given I select rows in any order (click order differs from list order), then the pipeline receives the selected items in list order with indexes 0..n-1 and an item count of n (so a rule that depends on position, such as numbering NS-17, follows the list, not the clicks).
5. Given the selection changes, then the preview updates and a `N of M files` count shows.

## Out of scope
- Anything not listed above.

## Decisions
- Checkboxes or selection? **Decision:** normal row selection, because it is what Explorer users know (po, 2026-10-18).
- No selection means every listed file is in the batch (criterion 1). Selecting every row (Ctrl+A) is the same batch as selecting none: the model normalises it to the whole list, so Ctrl+A from "nothing selected" causes no re-run. Clicking empty space under the rows clears the selection, so the batch goes back to the whole list (tech-lead default, 2026-10-18; po may overrule).
- A load (new folder, or the same folder reloaded) resets the selection to none; a rule change keeps it (tech-lead default, 2026-10-18; po may overrule).
- The count reads `{0} of {1} files` (`1 of 1 file` for a single item), numbers formatted `N0` in the current culture, shown at the left of the strip under the list whenever the list is loaded and has items, also with no selection (`12 of 12 files`). The conflict summary (NS-52) stays in the same strip, after the count. "files" covers folders too (tech-lead default, 2026-10-18; po may overrule).
- The old preview stays on screen until the new run is applied: the snapshot carries the batch it was computed from (its own row-to-index map), so cells keep showing the old run's rows during the debounce instead of blanking the column on every click (tech-lead default, 2026-10-18; po may overrule).
- The selection lives in `FileListModel` as an immutable `FileBatch`, rebuilt on every selection change and every load (tech-lead, 2026-10-18).
- A selection change recomputes after the same 150 ms debounce as a rule change (it shares the run, the cancellation and the version counter): holding Shift+Down or dragging must not start a 10 000-item run per row. The count does not wait: it reads the model's batch at once (tech-lead, 2026-10-18).
- Every native selection event only schedules one coalesced read on the UI thread; the read copies the list's selection once with `SelectedIndices.CopyTo` (O(k), never the indexer in a loop) and hands it to `FileListModel.SetSelection` (tech-lead, 2026-10-18).
- Ctrl+A is handled in `KeyDown` and selects every row with one `LVM_SETITEMSTATE` message on item -1; clearing on a folder load uses the same message with state 0. Rows are never selected one at a time (tech-lead, 2026-10-18).
- `ExistingNames` is not filtered by the selection, so an unselected file's name still counts as existing for `TargetExists` (tech-lead, 2026-10-18).

## Log
- 2026-10-18: created (po).
- 2026-10-18: set Ready, first round of the minimal usable slice (po).
- 2026-10-18: In Progress, branch feature/NS-14-selection (builder).
- 2026-10-18: In Review. The batch is in `FileListModel.Batch`, the snapshot carries the batch it ran on, a selection change recomputes after the 150 ms debounce, `N of M files` shows under the list, and NS-52 criterion 5 (an unselected file has no Status and still causes TargetExists) is now covered (builder).
