# NameShift: plan

Goal: a modern, maintained, lightweight replacement for the classic "panel of rename rules" bulk renamer on Windows.

## Constraints

- Installer of a few MB at most; the app has no external runtime besides the .NET Framework 4.8 that ships with Windows.
- No third-party UI framework; plain WinForms, high-DPI aware.
- The engine (`NameShift.Core`) has no UI dependency and every rule is unit-tested.

## Window

- Left: folder tree (drives, special folders). Top: path bar.
- Right: file list with columns Name, New Name (live preview, changed names highlighted), Size, Modified.
- Bottom: rule panels, each with an enable checkbox and a reset button, applied in the numbered order below.
- Bottom right: Reset, Revert (undo last batch), Rename.

## Rules, in pipeline order

1. RegEx: match / replace, optionally including the extension.
2. Name: keep, remove, fixed, reverse.
3. Replace: find / with, match case.
4. Case: same, lower, upper, title, sentence; exceptions list.
5. Remove: first n, last n, from–to, chars, words, crop before/after, digits, symbols, accents, high chars, trim, lead dots, double spaces.
6. Move/Copy parts: move or copy n chars from start/end to start/end/position, with separator.
7. Add: prefix, insert at position, suffix, word space.
8. Auto date: prefix/suffix with created/modified/EXIF date, format, separator, custom format.
9. Numbering: prefix/suffix/insert, start, increment, pad, break per folder, base (decimal, hex, letters), roman numerals.
10. Append folder name: prefix/suffix, levels, separator.
11. Extension: same, lower, upper, fixed, extra, remove.

Plus filters (mask, regex, folders/files/hidden/subfolders, length limits). NameShift only renames: it never copies, moves or deletes files (David, 2026-10-18).

## Build order

1. Engine: pipeline + rules 1–11 with tests; conflict detection (duplicate targets, invalid chars, too-long paths).
2. Window: tree, list, live preview, Rename, undo log.
3. Filters.
4. Save/load presets; remember last settings.
5. Installer (Inno Setup), code signing later, GitHub Releases.
