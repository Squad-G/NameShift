# NS-85: Read the photo dates of the listed files

Status: Ready
Depends on: NS-62, NS-27
Area: app

## Why
Give the preview the photo date of each JPEG, read off the UI thread and remembered until the file changes, so a big
folder is not read again at every keystroke. NS-62 is merged: `RenameItem.Taken` and `WithTaken` exist.

## Facts checked against dev (2026-10-25)
- `PhotoDate.Read(Stream)` (NS-27, `NameShift.Core`) returns `DateTime?`: DateTimeOriginal only, `Kind` Unspecified, null for
  any bad data, never seeks, consumes at most 262,144 bytes, lets an `IOException` of the stream through.
- `RenameItem.WithTaken(DateTime?)` returns a new item equal in every other fact (name, folder, kind, attributes, size,
  created, modified). `AutoDateRule` treats `null` and `default(DateTime)` as "no photo date" and uses the modified date.
- `DiskFileListSource` builds items with `Size` and `Modified` from the directory entry and never sets `Taken`; it is not
  changed by this story. `FileBatch.Items` (what the preview plans) is a list of `RenameItem`.
- `ItemAttributes` has only ReadOnly, Hidden and System: the item does not say whether a file is an online-only cloud
  placeholder, so the real file access checks that itself (criterion 2).

## Acceptance criteria
A class in `NameShift.Ui` (indicative name `PhotoDates`) over a small internal file-access seam, with tests on net10.0 and
net48. It does nothing by itself: the listing code is not changed and nothing creates it yet (NS-86 does).
1. Given `Fill(items, token)` (items an `IReadOnlyList<RenameItem>`), then it returns a new list in the same order. A file
   whose name ends in `.jpg` or `.jpeg` (ignoring case) comes back as `item.WithTaken(date)` where `date` is
   `PhotoDate.Read` of its start, or null. Every other item (other extensions, every folder, even a folder named `x.jpg`)
   comes back as the same instance and is never opened. The input list and its items are not changed. The date is stored
   as `PhotoDate.Read` returned it (no conversion).
2. Given a file, then it is opened for reading with sharing for read, write and delete (a photo open elsewhere still
   reads), forward-only (no seek needed), and `PhotoDate.Read` is given the stream (it consumes at most 256 KiB, whatever
   the file's size: a 2 GB file named `.jpg` costs the same as a small one). The real access first looks at the file's
   attributes: a file marked Offline, RecallOnOpen or RecallOnDataAccess (an online-only cloud placeholder) is not opened,
   so reading never makes a cloud service download it; it gives null and that null is not cached (criterion 3). An
   IOException (a missing file included), UnauthorizedAccessException, SecurityException or NotSupportedException gives
   null for that file only and the next files are still read. A file that is not a JPEG, or is empty, gives null.
3. Given the cache, then the key is the full path (ignoring case) with the size and the modified time (ticks) of the item
   as listed. A second `Fill` of the same items opens no file (counted through the seam). A changed size or modified time
   reads the file again. A null that came from the file's own bytes (not a JPEG, no Exif date, damaged) is cached too, so
   an item without a date is not reread. A null that came from a failure to read (the exceptions of criterion 2, a
   skipped placeholder) is not cached: the next `Fill` tries again, so a photo that was only locked is not marked "no date"
   until it changes.
4. Given the cache is full at 50,000 entries, then it is emptied before the next entry is added (simple, bounded), and the
   result of every `Fill` is still right. The cache is never written to disk and lives as long as the instance (the
   window keeps one for the process).
5. Given a cancelled token, then `Fill` throws `OperationCanceledException` before reading the next file (checked before
   each file, not once per 1,000); what was read stays cached.
6. Given two `Fill` calls at the same time (a superseded preview run may still be finishing), then both return correct
   lists and the cache is not corrupted (lock; a test runs both from the pool over 2,000 items). The lock is not held
   while a file is read (a slow file must not block the other call's cache hits).
7. Given a real temp folder and a small JPEG built in the test (Exif block with `2023:07:01 10:00:00`, written as bytes),
   then the real file access returns 2023-07-01 10:00:00 on both runtimes; a file locked for writing by another stream
   still reads; a file locked with no sharing gives null, with no exception, and is read again once the lock is gone. A
   200 KB file of junk named `.jpg`, a zero-byte `.jpg` and a text file named `.jpeg` give null.
8. Given 10,000 items listed (names `.jpg`, no file behind them), then `Fill` does not throw and returns 10,000 items; the
   PR notes the time on a folder of about 2,000 real photos on a local disk (written down, not asserted).

## Out of scope
- Using it in the preview, the panel, the note and the command line (NS-86).
- RAW, HEIC, PNG, TIFF, video; `.jpe` and `.jfif`.
- Reading files in parallel; a time limit on a slow share (a read blocked on a dead share cannot be cancelled, only
  superseded, as for the folder listing).

## Decisions
- Which files are read? **Decision:** names ending in `.jpg` or `.jpeg` only, so a folder of videos or documents is never
  opened by the preview. The common cases are covered and the rule stays one line (po, 2026-10-25).
- How is a stale date avoided? **Decision:** the cache key holds size and modified time, so an edited photo is read again
  and an untouched one never (po, 2026-10-25).
- Cache limit? **Decision:** 50,000 entries, emptied when full: a few MB at most, no eviction logic (po, 2026-10-25).
- Disk or memory cache? **Decision:** memory only, for the process: nothing about the user's photos is stored (po,
  2026-10-25).
- Which EXIF tag, in what order? **Decision:** DateTimeOriginal only, which `PhotoDate` already reads (NS-27); no second
  tag and no order of tags. No Exif date means no photo date, and NS-62's rule then uses the modified date (po,
  2026-10-25).
- New dependency or `System.Drawing`? **Decision:** none. `PhotoDate` is the clean-room reader written for NameShift; this
  story only opens the files and adds no NuGet package (po, 2026-10-25).
- Invalid or absent EXIF? **Decision:** null for that file and nothing else happens: no message, no exception, the rest of
  the batch is read (po, 2026-10-25).
- Huge or malformed JPEG? **Decision:** the reader stops after 256 KiB and checks every length (NS-27), so a 2 GB file or a
  hostile one cannot stall or crash the preview; this class adds no read of its own beyond `PhotoDate.Read` (po,
  2026-10-25).
- Cloud placeholders (online-only files)? **Decision:** never opened: reading them would download them behind the user's
  back; they count as having no photo date (po, 2026-10-25).
- Cache a failed read? **Decision:** no; only an answer from the bytes is remembered, so a locked photo is tried again at
  the next run (po, 2026-10-25).
- Read the whole list or the batch? **Decision:** `Fill` reads whatever it is given; NS-86 gives it the batch only (the
  selected rows, or every row), so unselected files are never opened (po, 2026-10-25).

## Log
- 2026-10-25: created from the second half of NS-62 (po).
- 2026-10-25: re-checked against dev after NS-62 (306e581): API facts recorded, cloud placeholders and failed-read caching
  decided, criteria tightened; set Ready (po).
