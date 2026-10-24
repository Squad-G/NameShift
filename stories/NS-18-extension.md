# NS-18: Extension rule

Status: Ready
Depends on: NS-1
Area: engine

## Why
Change or remove file extensions safely.

## Acceptance criteria
`ExtensionRule(ExtensionMode mode, string value = "")`, stage Extension (11), modes Same, Lower, Upper, Fixed, Extra,
Remove. Case changes are invariant-culture. Only the last extension (as split by NS-1) is looked at.
1. Given `a.JPG`, mode lower, then `a.jpg`; mode upper on `a.jpg` gives `a.JPG`; mode same changes nothing and
   `Enabled` is false.
2. Given mode fixed `png` on `a.jpg`, then `a.png`; spaces around the typed value are trimmed and dots at either end
   are ignored (`.png`, ` .png `, `..png`, `png.` all = `png`). An inner dot is kept: fixed `tar.gz` on `a.jpg` gives
   `a.tar.gz`. A value of only dots and spaces counts as empty.
3. Given mode extra `bak` on `a.txt`, then `a.txt.bak`; on `Makefile` then `Makefile.bak`; the same dot and space
   cleaning as fixed applies; an empty value changes nothing and `Enabled` is false.
4. Given mode remove on `a.txt`, then `a`; on `a.tar.gz` then `a.tar`; on `Makefile` and on `.gitignore` nothing
   changes.
5. Given mode fixed `png` on `Makefile`, then `Makefile.png` (the extension is added).
6. Given mode fixed with an empty value (or only dots and spaces), then the name is unchanged and `Enabled` is false,
   as in NS-19 (Remove is the way to drop extensions, so choosing Fixed before typing never strips them).
7. Given a folder `v1.2` and any mode, then the name is unchanged (a folder has no extension; fixed and extra never
   add one to a folder).
8. Given lower on `a.tar.GZ`, then `a.tar.gz` (the base name keeps its case); given upper on `Photo.jpg`, then
   `Photo.JPG`.
9. Given the rule runs after other stages (pipeline test), then Extension is last: Add suffix `_x` then fixed `png` on
   `a.jpg` gives `a_x.png`.
10. Given a value with a Windows-invalid character (`a.b?c`), then the rule applies it as typed and the existing
    invalid-name check flags the item (the rule never refuses or throws).
11. Given an unknown mode value, then the constructor throws `ArgumentOutOfRangeException` (as `CaseRule`).

## Out of scope
- The panel (NS-55).
- Converting file content, detecting the real file type.

## Decisions
- Does it ever rename the real file type? **Decision:** it only renames; the file content is not converted, and the UI says so (po, 2026-10-18). The panel (NS-55) shows that note.
- Folders? **Decision:** untouched, an extension on a folder is almost always a mistake (po, 2026-10-23).
- Extra on a file without extension? **Decision:** adds it, same as fixed; no special case (po, 2026-10-23).
- Typed dots and spaces? **Decision:** trimmed and dots at either end dropped, so `.png`, `png` and `png.` are the same (po, 2026-10-23).
- Fixed with an empty value? **Decision:** does nothing, like NS-19's Fixed, so an empty box never wipes extensions by
  accident; the draft's "behaves as remove" is withdrawn (po, 2026-10-23, from the PR #24 review).

## Log
- 2026-10-18: created (po).
- 2026-10-23: criteria made testable, folders and edge cases decided, set Ready (po).
