# NS-28: Append folder name rule

Status: Ready
Depends on: NS-1
Area: engine

## Why
Add the name of the parent folder(s) to a file name, for example to tell the photos of two trips apart.

## Acceptance criteria
`FolderNameRule(FolderNamePlacement placement = Prefix, int levels = 1, string separator = "_")`, stage FolderName
(10, after Numbering). The folders come from `RenameItem.FolderPath` (the item's containing folder; for a folder item,
its parent). Both `\` and `/` separate the segments, empty segments and a trailing separator are ignored. `Prefix` puts
the text in front of the base name; `Suffix` puts it after the base name, before the extension. The base name is the
only part that changes. Folder names are used as given (their case is kept).
1. Given `C:\Photos\Paris` as the folder, `a.jpg`, prefix, levels 1 and separator `_`, then `Paris_a.jpg`.
2. Given the same item, suffix, levels 2 and separator `-`, then `a-Photos-Paris.jpg`: the outer folder first when
   reading left to right, for both placements. Given prefix and levels 2 with `-`, then `Photos-Paris-a.jpg`.
3. Given levels greater than the number of folders (levels 5 on `C:\Photos\Paris`), then the available ones are used:
   `Photos_Paris_a.jpg`.
4. Given a file in a drive root (`C:\` or `C:`), then nothing is added and the name is unchanged (`Unchanged`).
5. Given any path, then the drive (`C:`, a first segment ending in a colon) is never used as a folder name: for
   `a.jpg` in `C:\` with levels 3, nothing is added; in `C:\x` with levels 3 and `_`, then `x_a.jpg`.
6. Given a network path (a `FolderPath` starting with two separators, `\\server\share\Docs\Trip`), then the first two
   segments (the server and the share) are never used, as the drive is not: only `Docs` and `Trip` can be. Given
   `\\server\share` itself, nothing is added.
7. Given levels below 1 (0 or negative), then `Enabled` is false and the name is unchanged; given levels of
   `int.MaxValue`, then it is clamped to the depth with no error. `Enabled` is true when levels >= 1.
8. Given an empty separator, then the parts are joined with nothing (`Parisa.jpg`); a null separator makes the
   constructor throw `ArgumentNullException`; an undefined placement makes it throw `ArgumentOutOfRangeException`.
9. Given a folder item `Sub` in `C:\Photos\Paris` with suffix and levels 1 and `_`, then `Sub_Paris` (a folder has no
   extension); given a hidden dotfile `.gitignore` with prefix, then `Paris_.gitignore` (the whole name is the base,
   NS-1).
10. Given the pipeline with Numbering suffix (stage 9) and this rule as a suffix (stage 10), on `a.jpg` in
    `C:\Photos\Paris`, then `a_1_Paris.jpg` when the numbering separator and this separator are both `_`: Numbering
    runs first.
11. Given an odd `FolderPath` (empty, `\`, `C:\\`, only separators), then it gives no folders and the rule does not
    throw. Given folder names that make the new name too long, then the rule applies them and the existing path-length
    check (NS-7) flags the item.
12. Given the properties, then `Placement`, `Levels` and `Separator` return the values given.

## Out of scope
- The panel (NS-31).
- Using the folder names of the destination: NameShift never moves files (NS-34 is cancelled).
- Looking at the disk: the rule only reads the path text in `FolderPath`.

## Decisions
- Order of several folders? **Decision:** outer folder first, as in the path read left to right, for prefix and suffix
  alike (po, 2026-10-23).
- Drive and network names? **Decision:** never used as a folder name (the drive, the server and the share), since
  `C:` or `server` is not a name users call a folder (po, 2026-10-23).
- Default separator? **Decision:** `_`, as in Numbering (po, 2026-10-23).
- Place relative to the extension? **Decision:** before it, so `.jpg` stays last (po, 2026-10-23).

## Log
- 2026-10-18: created (po).
- 2026-10-23: criteria made testable (order, placement, root and network paths, limits), set Ready (po).
