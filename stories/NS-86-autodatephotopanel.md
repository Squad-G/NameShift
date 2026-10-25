# NS-86: Auto date panel: the Photo date choice

Status: In Review
Depends on: NS-62, NS-85, NS-87, NS-59, NS-76
Area: app

## Why
Pick "Photo date" in the Auto date panel and see photos named by the day they were taken, with a clear sign on the files
that have no photo date. NS-62 and NS-85 are merged; NS-59 and NS-76 are Done. Start only after NS-87 is merged (both
edit `CommandRunner.RunApply`); re-read that method then.

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
- The command line (`CommandRunner.RunApply`) lists with `ListEntries(folder, false, ...)`, sorts with
  `FileListOrder.Sort`, filters with `options.Filter` (NS-41, merged), returns early on `No files match.`, then calls
  `RenamePlanner.Plan`; warnings and errors go to `_error`. NS-87 (`--recurse`) edits the same method: build it first
  (order: NS-87, then NS-86). In `RunApply` today: `items` is
  `FileListOrder.Sort(listing.Items).Where(filter.Includes).ToArray()`, then
  `RenamePlanner.Plan(BuildRules?.Invoke(options) ?? host.Rules, items, ...)`; the Auto date panel is in `host.Panels`,
  but the built rules decide whether photo dates are needed: an `AutoDateRule` with `Source == DateSource.Taken` among
  `BuildRules?.Invoke(options) ?? host.Rules` (criterion 3), not the panel's `Enabled` and `Source`.
- Re-checked after NS-85 (226ff59): `PhotoDates` is public with a public parameterless constructor (real disk, 50,000
  entries) and `Fill(IReadOnlyList<RenameItem>, CancellationToken)` returning a new list of the same length and order;
  `DiskPhotoFile` and `IPhotoFile` are the internal seam. `PreviewModel.Compute` is still static and takes no
  `PhotoDates`: the model gets one instance in its constructor (the window passes one for the process; the existing
  constructors keep working for tests with a default instance or a fake). Everything above still holds.

## Acceptance criteria
Logic in `NameShift.Ui` and `NameShift.Cmd` with tests on net10.0 and net48; the window code stays thin.
1. Given the Auto date panel, then Source lists `Modified date`, `Created date`, `Photo date` in this order
   (`AutoDatePanelModel.Sources`, `IndexOf`, `DateSourceText`); the default stays Modified; Reset and `IsDefault` as today.
2. Given a preset saved with source Photo date, then it stores `Taken` (the enum name, like the other two) and loads back
   to Photo date; a preset written before this story loads as before; an unknown name still falls back to Modified.
3. The one trigger, in the window and on the command line: the built rules (`RulePanelHostModel.Rules`, or what
   `CommandRunner.BuildRules` returns) contain an `AutoDateRule` with `Source == DateSource.Taken`. A panel that is on
   but builds no rule (empty or refused format) triggers nothing: no file is read and no note or warning appears.
   Given the trigger, the preview run (`PreviewModel`'s background compute, on the pool) fills the photo dates of the
   batch's items through NS-85 before planning; only the batch is read (unselected rows are not). The planner gets the
   filled items; `PreviewSnapshot.Batch` and `Items` stay the original list (the row mapping works, and `MainActions`
   still passes `snapshot.Items` to `RenameFolders`); update the doc comment "the exact list the pipeline ran on". With
   another source, or no rule, no file is opened (counted with a fake source). Editing the format or the separator runs
   again without reading any file, when the fake reads succeed (a failed read is not cached and is tried again, by NS-85
   design). The fill takes the run's cancellation token; a test records the thread id and shows the fill ran on a pool
   thread, not the caller's.
4. Given the trigger and a file row whose item has no photo date (not a JPEG, no Exif date, unreadable, a folder), then
   the Status cell shows `No photo date: modified date used` (new string in `NameShift.Ui.Strings`;
   `StatusCell.IsConflict` is false), Rename is not blocked by it, and the new name uses the modified date (NS-62). A row
   that also has a conflict shows the conflict text only. A row with a photo date shows nothing. The snapshot records at
   compute time whether photo dates were read (a flag on `PreviewSnapshot`, set by `Compute`), and the note follows that
   flag, never the live panel: an outdated snapshot never shows notes that do not match the names beside it.
   `RenameResult.Item` already carries the filled item, so the row has a photo date when `Results[i].Item.Taken` is
   neither null nor `default(DateTime)` (the same test as `AutoDateRule`); only the flag is new. `StatusCell` is not
   changed: `FileList` greys any non-empty Status cell that is not a conflict with `SystemColors.GrayText` (readable in
   a Windows contrast theme), never the conflict colour (a one-line rule, checked by hand).

5. Given `apply --date-source taken` on the command line, then it is accepted (`DateSourceValues` becomes `modified, created
   or taken`, in the style of the other values texts, and the apply help line reads `--date-source modified|created|taken`),
   the photo dates are filled before planning with the same class (after the filter, so only the files that will be
   planned are read; only on the trigger of criterion 3, so no other run opens a file), and a name uses the photo date.
   A file without one uses its modified date and, once (counting only the files planned, not every file listed), one
   line goes to the error stream: `N files have no photo date; their modified date was used.` (one: `1 file has no
   photo date; its modified date was used.`). N is plain digits with `N0` grouping like NS-87's `SkippedFoldersNotice`.
   Placement: right after the fill, after NS-87's skipped-folders line, before any plan output; it is written whatever
   the plan turns out to be (Blocked, Nothing to rename, dry run or rename, because the names were built with the
   fallback), and not written when `No files match` returned first. The exit code is not changed by the line and
   `--quiet` does not hide it (warnings are not success output; NS-41 decision). Test seam: an internal settable
   `PhotoDates` property on `CommandRunner`, next to `BuildRules`, that the tests set to a `PhotoDates` over a fake
   `IPhotoFile` (the `Cmd.Tests` project sees Ui internals); left null, `RunApply` creates one `PhotoDates` per run.
6. Given `--save-preset` with `--date-source taken`, then the saved preset holds `Taken` (criterion 2). Traced on
   226ff59: `SaveSettings` already writes `Source.ToString()` ("Taken") and `ApplySettings` loads it only when
   `IndexOf(source) >= 0`, so adding `Taken` to `SourceList` is the whole fix for save and load; `IsDefault` and Reset
   (`Source = Modified`) are unchanged. A test saves with Taken, loads it, and checks the window's panel shows Photo date.
7. Given the Rename of such a batch, then the journal and Revert work as for any batch (a hand check with two JPEGs, one
   with no Exif date).
8. By hand, written in the PR: a folder of 2,000 photos with Photo date chosen stays responsive (the list does not freeze
   while typing in the format box), and the row note shows on the file without a date.
9. By hand, written in the PR (limits of NS-85, not fixable without a handle-level check; the PR notes what was seen):
   a. A OneDrive (or similar) online-only `.jpg` shows the note and is not downloaded. b. Known gap: a file that turns
   into a placeholder between the attribute check and the open is recalled; a file symlink to a placeholder shows only
   ReparsePoint and is opened. Neither is tested or fixed here.
10. Items given to `PhotoDates.Fill` come from a folder listing (the file list's items, or `ListEntries` on the command
   line), never built by hand: an item with invalid path characters can throw `ArgumentException` on net48. No new code
   path builds an item for it.
11. Existing tests that change (the builder updates them, not deletes them): `RegexMoveDateFolderParserTests` (about line
   150: `--date-source taken` is a bad choice today), `CmdStringsTests` (about line 82: the `DateSourceValues` text),
   the help-text tests that hold the apply help line, `WindowParityRegexMoveDateFolderTests` (add a Taken row),
   `AutoDatePanelModelTests` (the list of two becomes three), and `CommandLineParserTests` if it names the values.

## Out of scope
- Other date tags (DateTimeDigitized, DateTime), time zones, other file types.
- A setting to turn the fallback into an error.

## Decisions
- Show the note where? **Decision:** in the existing Status column, in the grey text colour (`SystemColors.GrayText`), so
  no new column and no dialog (po, 2026-10-25).
- Warning on the command line? **Decision:** one line on the error stream, never hidden by `--quiet`, exit code unchanged:
  a script keeps working and a person sees it (po, 2026-10-25).
- Read the unselected rows? **Decision:** no, only the batch the preview plans (po, 2026-10-25).

- Accept the two placeholder gaps? **Decision:** yes, as hand-check notes only: they need a handle-level check, and a
  rare recall of one file is not worth the code (po, 2026-10-25).
- Who owns the `PhotoDates` instance? **Decision:** one per process in the window (so the cache lives), one per run on the
  command line (po, 2026-10-25).

## Log
- 2026-10-25: created from the second half of NS-62 (po).
- 2026-10-25: re-checked against dev after NS-62 (306e581): stale facts fixed (what NS-62 already did, the snapshot batch
  rule, the CLI help and values texts, the note colour); stays Draft until NS-85 is merged (po).
- 2026-10-25: NS-41 merged (3dca426): the command-line fact refreshed; build after NS-87 (same method of
  `CommandRunner`); still Draft until NS-85 is merged (po).
- 2026-10-25: NS-85 merged (226ff59): every criterion re-checked against the merged code, facts refreshed, placeholder
  gaps and the listing rule folded into criteria 9 and 10; depends on NS-87 (same `RunApply`); set Ready (po).
- 2026-10-25: PR #104 review nits folded in: one trigger (built `AutoDateRule` with Taken), snapshot flag at compute
  time, grey rule in `FileList`, `CommandRunner.PhotoDates` seam, warning placement and format, clean-read and
  pool-thread test notes, the list of existing tests that change, preset trace; BACKLOG rows swapped (po).
- 2026-10-25: from the PR #103 review (NS-85), for the hand checks: a file that becomes an online-only placeholder
  between the attribute check and the open is recalled, and a file symlink whose target is a placeholder is opened (it
  shows only ReparsePoint); items given to PhotoDates.Fill must come from a listing (tech-lead).
- 2026-10-25: PR #104 re-review nits folded in: the command-line fact says the built rules decide (not the panel's
  Enabled and Source), and criterion 11 moved after 10 (tech-lead).
- 2026-10-25: built (builder): Photo date in the panel (Sources, presets), PhotoDateNeed trigger, snapshot flag and grey Status note, `apply --date-source taken` with one error-stream line for planned files without a photo date; In Review.
