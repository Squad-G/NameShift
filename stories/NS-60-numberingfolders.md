# NS-60: Numbering rule: restart in each folder

Status: Draft
Depends on: NS-17
Area: engine

## Why
When the list holds files from several folders (sub-folders, NS-33), number each folder from the start value again. Set
`Ready` when NS-33 is next: until then the window lists one folder and the option has no use.

## Acceptance criteria
`ItemContext` gets `int FolderIndex`: the 0-based position of the item among the items of the same folder in the list
the pipeline was given. The constructor becomes `ItemContext(RenameItem item, int index, int count, int? folderIndex =
null)`; null means the same as `index` (every existing caller and test is unchanged); a value outside `0..index` throws
`ArgumentOutOfRangeException`. `RenamePipeline.Run` computes it for each item. Two items are in the same folder when
their `FolderPath` is equal ignoring case, after removing trailing `\` and `/` (`C:\Photos` and `c:\photos\` are one
folder; Windows paths do not depend on case). `NumberingRule` gets a last optional parameter `bool breakPerFolder =
false`, property `BreakPerFolder`: when true the number is `Start + FolderIndex * Step` instead of `Start + Index *
Step`.
1. Given `x\a.txt`, `x\b.txt`, `y\a.txt`, `y\b.txt` (folders `C:\x` and `C:\y`), Numbering Suffix, start 1, separator
   `_`, `breakPerFolder` true, then `a_1`, `b_2` in `x` and `a_1`, `b_2` in `y`. With `breakPerFolder` false (default),
   `a_1`, `b_2`, `a_3`, `b_4`.
2. Given an interleaved list `C:\x\a.txt`, `C:\y\a.txt`, `C:\x\b.txt`, then the numbers are 1, 1, 2: each folder counts its
   own items in list order.
3. Given `C:\Photos`, `c:\photos\` and `C:\PHOTOS` (a trailing separator, other case), then they are one folder: 1, 2, 3.
   Given `C:\a` and `C:\a b`, then two folders.
4. Given a start of 10 and a step of 5, then each folder gives 10, 15, 20; given a negative step that goes below zero in
   one folder, only that folder's items beyond the limit get the NS-17 error.
5. Given a single folder, then `breakPerFolder` true and false give the same names. Given an empty `FolderPath` for
   all items, then one folder.
6. Given a list of 10,000 items in 100 folders (item i in folder `i % 100`), then each item's `FolderIndex` is
   `i / 100`, and the run finishes (no time limit is tested, as in NS-58; the reviewer checks that `FolderIndex` is
   computed in one pass, not by a scan per item).
7. Given `ItemContext` built without `folderIndex`, then `FolderIndex` equals `Index`; given `folderIndex` 3 with index 2,
   then the constructor throws.
8. Given the option on with hexadecimal or Roman numerals (NS-29), then the numbers restart in each folder the same way.

## Out of scope
- The panel checkbox (NS-61) and the sub-folders option (NS-33).
- Sorting items by folder: the app gives the list order.

## Decisions
- How does a rule know the folder? **Decision:** the pipeline computes `FolderIndex` once for the whole list and puts it in
  `ItemContext`, because a rule sees only one item (po, 2026-10-24).
- Same folder? **Decision:** same path ignoring case and trailing separators, as Windows does (po, 2026-10-24).

## Log
- 2026-10-18: created inside NS-29 (po).
- 2026-10-24: split out of NS-29 as its own story; kept Draft until sub-folders (NS-33) are scheduled (po).
