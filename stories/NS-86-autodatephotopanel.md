# NS-86: Auto date panel: the Photo date choice

Status: Draft
Depends on: NS-62, NS-85, NS-59, NS-76
Area: app

## Why
Pick "Photo date" in the Auto date panel and see photos named by the day they were taken, with a clear sign on the files
that have no photo date. Second wave: set to Ready when NS-85 is merged (NS-62 is merged; NS-59 and NS-76 are Done).

## Facts checked against dev (2026-10-25)
- Already there from NS-62: `DateSource.Taken`, `AutoDateRule` using `RenameItem.Taken` (null or `default(DateTime)` falls
  back to the modified date), `DateSourceText.For` with a `Taken` case and the string `AutoDateSourceTaken` ("Photo date").
  Not done yet: `AutoDatePanelModel.SourceList` is `{ Modified, Created }` (the setter refuses anything not in it, and a
  saved `Taken` therefore loads as Modified today), `AutoDateReader` knows `modified` and `created` only, and the CLI texts
  `DateSourceValues` ("modified or created") and the help line `--date-source modified|created` say so.
- `PreviewModel.Compute` is a static method run on the pool: `RenamePlanner.Plan(rules, batch.Items, existing, token)`
  wrapped in `new PreviewSnapshot(batch, plan)`. `TryGetRow` compares `Snapshot.Batch.ListItems` with the file list's
  `Items` by reference, and `FileBatch.Items` is the same reference as `ListItems` for a whole-list batch: the snapshot's
  `Batch` must stay the original batch, and the filled items are only what the planner gets.
- `StatusCell(text, isConflict)`; `PreviewModel.StatusForRow` builds it from the conflicts only; `FileList` colours a
  conflict row with `Palette.ConflictText` and has no colour for a note (`Palette` has no muted colour).
- The command line (`CommandRunner.Run` apply path) lists with `ListEntries(folder, false, ...)`, sorts with
  `FileListOrder.Sort`, filters with `ItemFilter` (NS-41 replaces that), then calls `RenamePlanner.Plan`; warnings and
  errors go to `_error`.

## Acceptance criteria
Logic in `NameShift.Ui` and `NameShift.Cmd` with tests on net10.0 and net48; the window code stays thin.
1. Given the Auto date panel, then Source lists `Modified date`, `Created date`, `Photo date` in this order
   (`AutoDatePanelModel.Sources`, `IndexOf`, `DateSourceText`); the default stays Modified; Reset and `IsDefault` as today.
2. Given a preset saved with source Photo date, then it stores `Taken` (the enum name, like the other two) and loads back
   to Photo date; a preset written before this story loads as before; an unknown name still falls back to Modified.
3. Given the preview with an Auto date rule enabled and the source Photo date, then the preview run (`PreviewModel`'s
   background compute, on the pool) fills the photo dates of the batch's items through NS-85 before planning; only the
   batch is read (unselected rows are not). The snapshot keeps the original `Batch` (the row mapping still works). With
   another source, or the rule off, no file is opened (counted with a fake source). Editing the format or the separator
   runs again without reading any file (the cache answers). The fill takes the run's cancellation token.
4. Given that rule and a file row whose item has no photo date (not a JPEG, no Exif date, unreadable, a folder), then the
   Status cell shows `No photo date: modified date used` (new string in `NameShift.Ui.Strings`; `StatusCell.IsConflict`
   is false), Rename is not blocked by it, and the new name uses the modified date (NS-62). A row that also has a
   conflict shows the conflict text only. A row with a photo date shows nothing. The note shows for a row in the
   snapshot's batch when an Auto date rule is enabled with source Photo date and the item has no photo date (nothing else
   decides it). `PreviewSnapshot` carries that information (the filled items or a per-row flag), since `Batch` stays the
   original. In the list the cell uses `SystemColors.GrayText` (readable in a Windows contrast theme), never the conflict
   colour.
5. Given `apply --date-source taken` on the command line, then it is accepted (`DateSourceValues` becomes `modified, created
   or taken`, in the style of the other values texts, and the apply help line reads `--date-source modified|created|taken`),
   the photo dates are filled before planning with the same class (after the filter, so only the files that will be
   planned are read), and a name uses the photo date. A file without one uses its modified date and, once (counting only
   the files planned, not every file listed) before the
   rename or the dry run, one line goes to the error stream: `N files have no photo date; their modified date was used.`
   (one: `1 file has no photo date; its modified date was used.`). The exit code is not changed by the line and `--quiet`
   does not hide it (warnings are not success output; NS-41 decision).
6. Given `--save-preset` with `--date-source taken`, then the saved preset holds `Taken` (criterion 2).
7. Given the Rename of such a batch, then the journal and Revert work as for any batch (a hand check with two JPEGs, one
   with no Exif date).
8. By hand, written in the PR: a folder of 2,000 photos with Photo date chosen stays responsive (the list does not freeze
   while typing in the format box), and the row note shows on the file without a date.

## Out of scope
- Other date tags (DateTimeDigitized, DateTime), time zones, other file types.
- A setting to turn the fallback into an error.

## Decisions
- Show the note where? **Decision:** in the existing Status column, in the grey text colour (`SystemColors.GrayText`), so
  no new column and no dialog (po, 2026-10-25).
- Warning on the command line? **Decision:** one line on the error stream, never hidden by `--quiet`, exit code unchanged:
  a script keeps working and a person sees it (po, 2026-10-25).
- Read the unselected rows? **Decision:** no, only the batch the preview plans (po, 2026-10-25).

## Log
- 2026-10-25: created from the second half of NS-62 (po).
- 2026-10-25: re-checked against dev after NS-62 (306e581): stale facts fixed (what NS-62 already did, the snapshot batch
  rule, the CLI help and values texts, the note colour); stays Draft until NS-85 is merged (po).
