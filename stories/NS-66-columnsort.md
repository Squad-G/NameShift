# NS-66: Column sorting

Status: Draft
Depends on: NS-51, NS-14, NS-13
Area: app

## Why
Order the list by name, size or date, and number the files in that order. (Split out of NS-46.)

## Acceptance criteria
Not final; to be made testable when it is scheduled. It will cover: clicking the Name, Size or Modified header sorts the
list (Name natural, as `FileListOrder` does today; Size and Modified numeric and by date, ties broken by the natural name);
a second click reverses it and the header shows an arrow; the list order is the pipeline order, so numbering follows it
(NS-17); the selection is kept by item (not by row) and the New Name preview is recomputed once; a folder load goes back to
the default order (folders first, natural name); folders stay first in every order unless a decision says otherwise; New
Name and Status headers are not sortable. The logic goes in `FileListModel` (the sort state and a re-sort that keeps
`Batch` consistent with `PreviewModel.TryGetRow`), the header click and arrow in `FileList`.

## Out of scope
- Sorting by the New Name column (it changes as the rules change, which would reorder rows while typing).
- Remembering the order between runs (NS-37).

## Decisions
- Folders first? **Decision:** yes in every order, as in Explorer's default, so numbering never mixes folders into files
  by surprise (po, 2026-10-24).

## Log
- 2026-10-24: split out of NS-46 (po).
