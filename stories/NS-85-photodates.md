# NS-85: Read the photo dates of the listed files

Status: Draft
Depends on: NS-62, NS-27
Area: app

## Why
Give the preview the photo date of each JPEG, read off the UI thread and remembered until the file changes, so a big
folder is not read again at every keystroke. Second wave: set to Ready when NS-62 is merged (it needs `RenameItem.WithTaken`).

## Acceptance criteria
A class in `NameShift.Ui` (indicative name `PhotoDates`) over a small internal file-access seam, with tests on net10.0 and
net48. It does nothing by itself: the listing code is not changed.
1. Given `Fill(items, token)`, then it returns a new list in the same order. A file whose name ends in `.jpg` or `.jpeg`
   (ignoring case) comes back as `item.WithTaken(date)` where `date` is `PhotoDate.Read` of its start, or null. Every
   other item (other extensions, every folder, even a folder named `x.jpg`) comes back as the same instance and is never
   opened.
2. Given a file, then it is opened for reading with sharing for read, write and delete (a photo open elsewhere still
   reads), and `PhotoDate.Read` is given the stream (it consumes at most 256 KiB). An IOException,
   UnauthorizedAccessException, SecurityException or NotSupportedException gives null for that file only and the next files
   are still read. A file that is not a JPEG, or is empty, gives null.
3. Given the cache, then the key is the full path (ignoring case) with the size and the modified time of the item as
   listed. A second `Fill` of the same items opens no file (counted through the seam). A changed size or modified time
   reads the file again. A null result is cached too (an item without a date is not reread).
4. Given the cache is full at 50,000 entries, then it is emptied before the next entry is added (simple, bounded), and the
   result of every `Fill` is still right. The cache is never written to disk and lives as long as the process.
5. Given a cancelled token, then `Fill` throws `OperationCanceledException` before reading the next file; what was read
   stays cached.
6. Given two `Fill` calls at the same time (a superseded preview run may still be finishing), then both return correct
   lists and the cache is not corrupted (lock; a test runs both from the pool over 2,000 items).
7. Given a real temp folder and a small JPEG built in the test (Exif block with `2023:07:01 10:00:00`, written as bytes),
   then the real file access returns 2023-07-01 10:00:00 on both runtimes; a file locked for writing by another stream
   still reads; a file locked with no sharing gives null, with no exception.
8. Given 10,000 items listed (names `.jpg`, no file behind them), then `Fill` does not throw and returns 10,000 items; the
   PR notes the time on a folder of about 2,000 real photos on a local disk (written down, not asserted).

## Out of scope
- Using it in the preview, the panel, the note and the command line (NS-86).
- RAW, HEIC, PNG, TIFF, video; `.jpe` and `.jfif`.

## Decisions
- Which files are read? **Decision:** names ending in `.jpg` or `.jpeg` only, so a folder of videos or documents is never
  opened by the preview. The common cases are covered and the rule stays one line (po, 2026-10-25).
- How is a stale date avoided? **Decision:** the cache key holds size and modified time, so an edited photo is read again
  and an untouched one never (po, 2026-10-25).
- Cache limit? **Decision:** 50,000 entries, emptied when full: a few MB at most, no eviction logic (po, 2026-10-25).
- Disk or memory cache? **Decision:** memory only, for the process: nothing about the user's photos is stored (po,
  2026-10-25).

## Log
- 2026-10-25: created from the second half of NS-62 (po).
