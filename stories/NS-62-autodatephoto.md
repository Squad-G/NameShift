# NS-62: Auto date: the photo date in the engine

Status: In Progress
Depends on: NS-26, NS-27
Area: engine

## Why
Let the Auto date rule name a photo by the day it was taken. This story is the engine only: the rule, the item and the
fallback. Reading the files is NS-85; the panel choice, the preview note and the command line are NS-86. Nothing in the
window or the command line changes until NS-86.

## Acceptance criteria
`NameShift.Core` only, tests on net10.0 and net48. Names of the new members are fixed here; everything else stays.
1. Given `DateSource`, then it has a third value `Taken` after `Created`; `Modified` and `Created` keep their numbers.
2. Given `RenameItem`, then it has a new last optional constructor parameter `DateTime? taken = null` and a property
   `Taken` (the camera's clock as `PhotoDate.Read` returns it, no conversion). Every existing call compiles and gives
   `Taken` null. `WithTaken(DateTime? taken)` returns a new item equal to this one in every other fact; the original is
   unchanged.
3. Given source `Taken`, an item `IMG_001.JPG` with `Taken` 2023-07-01 10:00:00 and Modified 2024-03-09 14:05:07, format
   `yyyy-MM-dd`, prefix, separator `_`, then the new name is `2023-07-01_IMG_001.JPG`. With format `yyyyMMdd_HHmmss`,
   suffix and separator `-`, then `IMG_001-20230701_100000.JPG`.
4. Given source `Taken` and an item with `Taken` null and Modified 2024-03-09 14:05:07, then the rule uses the modified
   date (`2024-03-09_IMG_001.JPG`), with no error: a photo without a date is not a failure.
5. Given source `Taken`, `Taken` null and Modified left at its default, then the rule throws the same
   `InvalidOperationException` as for the other sources, so the pipeline reports the same row error.
6. Given source `Modified` or `Created` and an item that also has a `Taken` date, then `Taken` is ignored.
7. Given a `Taken` value of kind Utc or Local, then its digits are formatted as they are (no time-zone change), like the
   other two dates.
8. Given `DateSourceText.For(DateSource.Taken)`, then it returns the new string `AutoDateSourceTaken` = `Photo date`
   (`Strings.resx` and `Strings.cs`); an undefined value still throws `ArgumentOutOfRangeException`.
9. Given the Auto date panel, presets and the command line, then they are not changed by this story (the panel's source
   list stays Modified and Created until NS-86).

## Out of scope
- Reading files, a cache, the panel choice, a note on the row, the command line (NS-85, NS-86).
- Other EXIF tags and file types.

## Decisions
- No photo date: error or fall back? **Decision:** fall back to the modified date in the engine, so the window and the
  command line agree. An error would block Rename for the whole batch (NS-15) because of one screenshot in the folder. The
  user is told per row in NS-86 (po, 2026-10-25).
- Where does the photo date live? **Decision:** a plain optional fact on `RenameItem`, set by the caller, so `NameShift.Core`
  never touches the disk (po, 2026-10-25).
- Should the note be an engine result? **Decision:** no: the window derives it from the item (NS-86), so `RenameResult` and
  the conflict checks stay as they are (po, 2026-10-25).

## Log
- 2026-10-24: created when NS-27 was split: this is the rule and wiring part (po).
- 2026-10-25: re-scoped to the engine only; reading and wiring moved to NS-85 and NS-86; fallback decided; set Ready (po).
- 2026-10-25: In Progress (builder).
