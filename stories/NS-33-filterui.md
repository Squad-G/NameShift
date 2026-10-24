# NS-33: Filters panel and sub-folders option

Status: Draft
Depends on: NS-32, NS-51, NS-60, NS-61
Area: app

## Why
Filter the list from the window and include sub-folders.

## Acceptance criteria
1. Given the filter panel, then every filter option is editable and the file list updates live.
2. Given the sub-folders option on, then files of all sub-folders are listed with their relative folder shown, and renamed in place.
3. Given the folder is huge, then listing sub-folders stays cancelable and the window responsive (checked by hand).
4. Given the app starts or the filter panel is reset, then the sub-folders option is off.
5. Given the Numbering panel, then it has a "restart in each folder" check box (off by default, part of `IsDefault` and
   reset) that sets `breakPerFolder` of the rule (NS-60); with sub-folders on, numbers restart in each folder (to be made
   testable when scheduled).

## Out of scope
- Anything not listed above.

## Decisions
- Cycles through junctions? **Decision:** reparse points (junctions, links) are never followed, to avoid loops and surprises (po, 2026-10-18).
- Hidden files? **Decision:** the listing then returns hidden and system entries too (with their attributes) and `ItemFilter`
  (NS-32) leaves them out by default, so the "show hidden" option is one switch in the filter, not a second listing. This
  replaces `DiskFileListSource.IsShown`; `ExistingNames` keeps every entry as today (po, 2026-10-24).
- Split when scheduled: (a) filter settings model and sub-folder listing in `NameShift.Ui` with tests (a `FilterPanelModel`
  with plain properties and `Changed`/`Reset`/`IsDefault`, `FileListModel` applying `ItemFilter`, a recursive
  `IFileListSource` that skips reparse points and can be cancelled), then (b) the window control, and the "restart in each
  folder" checkbox in the Numbering panel: it adds `RestartPerFolder` to `NumberingPanelModel` on top of NS-61 and passes
  `breakPerFolder` (NS-60, built first). NS-33 owns that checkbox (po, 2026-10-24).
- Where does the filter control sit in the window? **Open, for David:** the recommended default is one compact row above
  the file list (mask box, kind check boxes, hidden, sub-folders, a "more" part for the regex and the lengths), because
  the rule panels at the bottom are for rules and filters act on the list. Draft until David answers (po, 2026-10-24).

## Log
- 2026-10-18: created (po).
- 2026-10-24: kept Draft for the placement question above; NS-32 (engine) is Ready first (po).
