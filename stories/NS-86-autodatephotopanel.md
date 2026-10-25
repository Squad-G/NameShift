# NS-86: Auto date panel: the Photo date choice

Status: Draft
Depends on: NS-62, NS-85, NS-59
Area: app

## Why
Pick "Photo date" in the Auto date panel and see photos named by the day they were taken, with a clear sign on the files
that have no photo date. Second wave: set to Ready when NS-62 and NS-85 are merged.

## Acceptance criteria
Logic in `NameShift.Ui` and `NameShift.Cmd` with tests on net10.0 and net48; the window code stays thin.
1. Given the Auto date panel, then Source lists `Modified date`, `Created date`, `Photo date` in this order
   (`AutoDatePanelModel.Sources`, `IndexOf`, `DateSourceText`); the default stays Modified; Reset and `IsDefault` as today.
2. Given a preset saved with source Photo date, then it stores `Taken` (the enum name, like the other two) and loads back
   to Photo date; a preset written before this story loads as before; an unknown name still falls back to Modified.
3. Given the preview with the Auto date rule enabled and the source Photo date, then the preview run (`PreviewModel`'s
   background compute) fills the photo dates through NS-85 before planning. With another source, or the rule off, no file
   is opened (counted with a fake source). Editing the format or the separator runs again without reading any file (the
   cache answers).
4. Given that rule and a file row whose item has no photo date (not a JPEG, no Exif date, unreadable, a folder), then the
   Status cell shows `No photo date: modified date used` (new string, not a conflict: `IsConflict` is false), Rename is
   not blocked by it, and the new name uses the modified date (NS-62). A row that also has a conflict shows the conflict
   text only. A row with a photo date shows nothing.
5. Given `apply --date-source taken` on the command line, then it is accepted (the values text becomes `modified, created,
   taken`), the photo dates are filled before planning with the same class, and a name uses the photo date. A file without
   one uses its modified date and, once before the rename or the dry run, one line goes to the error stream: `N files have
   no photo date; their modified date was used.` (one: `1 file has no photo date; its modified date was used.`). The exit
   code is not changed by the line and `--quiet` does not hide it (warnings are not success output).
6. Given `--save-preset` with `--date-source taken`, then the saved preset holds `Taken` (criterion 2).
7. Given the Rename of such a batch, then the journal and Revert work as for any batch (a hand check with two JPEGs, one
   with no Exif date).
8. By hand, written in the PR: a folder of 2,000 photos with Photo date chosen stays responsive (the list does not freeze
   while typing in the format box), and the row note shows on the file without a date.

## Out of scope
- Other date tags (DateTimeDigitized, DateTime), time zones, other file types.
- A setting to turn the fallback into an error.

## Decisions
- Show the note where? **Decision:** in the existing Status column, in the muted text colour of other non-conflict rows, so
  no new column and no dialog (po, 2026-10-25).
- Warning on the command line? **Decision:** one line on the error stream, never hidden by `--quiet`, exit code unchanged:
  a script keeps working and a person sees it (po, 2026-10-25).

## Log
- 2026-10-25: created from the second half of NS-62 (po).
