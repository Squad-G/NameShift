# NS-26: Auto date rule (file dates)

Status: In Review
Depends on: NS-1
Area: engine

## Why
Add the file's created or modified date to its name.

## Acceptance criteria
Two new public enums in Core, `DateSource { Modified, Created }` and `DatePlacement { Prefix, Suffix }`, and
`AutoDateRule(DateSource source = Modified, string format = "yyyy-MM-dd", DatePlacement placement = Prefix,
string separator = "_")`, stage AutoDate (8: after Add, before Numbering). The date comes from `RenameItem.Modified` or
`RenameItem.Created`, exactly as the app read it (local time, NS-1): the rule never converts time zones. The date text
is the .NET custom date format applied with `CultureInfo.InvariantCulture` (Gregorian calendar, English names, `AM`/`PM`),
whatever the user's culture. A format of one character (`d`, `M`, `y`) is read as a custom format, not as a .NET
standard format (so `d` gives the day of the month, `9`). Prefix gives `date + separator + base name`; Suffix gives
`base name + separator + date`. Only the base name changes. Two public statics are shared with the panel (NS-59):
`AutoDateRule.FormatDate(DateTime date, string format)` (the text the rule would add; an empty format gives `""`, not
.NET's general date text; throws `FormatException` for a format .NET refuses or one holding a time-zone letter, and
`ArgumentNullException` for null) and `AutoDateRule.Validate(string format)` (null when the format is empty or usable,
else a non-empty message; throws `ArgumentNullException` for null). The time-zone specifiers `K` and every run of `z` (`z`, `zz`, `zzz`, `zzzz` and longer)
are refused when they are not inside quotes (`'...'` or `"..."`) or escaped by a backslash: they give the PC's own zone
(`-05:00`, and `zzz` holds a colon), so they would make names depend on the PC. Unless a criterion says otherwise, the item is `a.txt`,
modified 2024-03-09 14:05:07.
1. Given modified 2024-03-09 14:05:07, format `yyyy-MM-dd`, Prefix, separator `_`, on `a.txt`, then `2024-03-09_a.txt`.
2. Given the same date, format `yyyyMMdd_HHmmss`, Suffix, separator `_`, then `a_20240309_140507.txt`.
3. Given Created 2020-01-02 10:00:00 and Modified 2024-03-09 14:05:07, source Created, then `2020-01-02_a.txt`; source
   Modified gives `2024-03-09_a.txt`.
4. Given the format `HH:mm`, then the result is `14:05_a.txt`, applied as typed, and the invalid-name check flags it
   (`InvalidCharacter`, pipeline test with `InvalidNameCheck`); the same with a format that gives `/` (`yyyy/MM/dd`).
5. Given `CultureInfo.CurrentCulture` set to French (`fr-FR`) for the test, format `MMMM dd` gives `March 09` and
   `dddd` gives `Saturday` (restore the culture afterwards); given a Thai culture (`th-TH`, Buddhist calendar) the year
   in `yyyy` is still `2024`.
6. Given a date whose `Kind` is Utc, then the clock values it holds are used as they are (no conversion): 2024-03-09
   14:05:07 gives `14-05-07` with `HH-mm-ss`.
7. Given an item whose chosen date is `default(DateTime)` (the app had none, as `FileListText` treats it), then that item
   gets a rename `Error` (`The item has no date to use.`, thrown as `InvalidOperationException`) and keeps its name;
   the other items of the run still get theirs.
8. Given the format `'abc` or `yyyy\` (a quote or backslash left open), then `Validate` returns a non-empty message and
   the constructor throws `ArgumentException`; `yyyy-MM-dd` and `""` give null. The same for `K`, `z`, `zz`, `zzz`, `zzzz`, `zzzzzzz`, and
   `yyyy-MM-dd zzz`; but `yyyy'z'` (quoted), `yyyy\z` (escaped) and `yyyy-MM-dd` are accepted (`yyyy'z'` gives `2024z`).
   `FormatDate(date, "")` gives `""`; `FormatDate(date, "zzz")` throws `FormatException`. Given a null format, the constructor and
   `Validate` throw `ArgumentNullException`. The message wording is .NET's and may differ between runtimes: tests check
   only that it is not empty.
9. Given an empty format, then the rule does nothing and `Enabled` is false. Given the format `''` (an empty quoted
   text), then the date text is empty, the name is unchanged and no separator is added.
10. Given the format `d`, modified 2024-03-09, then the date text is `9`; given `yyyy` then `2024`.
11. Given an empty separator, then `20240309a.txt` (format `yyyyMMdd`, Prefix); a null separator makes the constructor
    throw `ArgumentNullException`; an undefined source or placement makes it throw `ArgumentOutOfRangeException`.
12. Given a hidden dotfile `.gitignore`, Prefix, then `2024-03-09_.gitignore` (the whole name is the base, NS-1); given a
    folder `Sub`, Suffix, then `Sub_2024-03-09` (a folder has no extension, and its dates are used).
13. Given the pipeline with `AddRule(suffix: "-x")` (stage 7), this rule as a Suffix with separator `_` (stage 8) and
    `NumberingRule(NumberingMode.Suffix, separator: "_")` (stage 9), on `a.txt` modified 2024-03-09, then
    `a-x_2024-03-09_1.txt`, whatever order the rules were given.
14. Given the properties, then `Source`, `Format`, `Placement` and `Separator` return the values given.

## Out of scope
- The photo (EXIF) date (NS-27 reads it; NS-62 uses it here).
- The panel, the list of common formats and the example (NS-59).
- Converting to UTC or another zone; the date of "today".

## Decisions
- Date source time zone? **Decision:** local time, which matches what Explorer shows; the app already reads local times
  (po, 2026-10-18).
- Culture? **Decision:** always the invariant culture, so a batch gives the same names on every PC and a user can share a
  preset (po, 2026-10-18; the `th-TH` case added 2026-10-24).
- Time-zone letters (`K` and any run of `z`)? **Decision:** refused by `Validate`, so the claim above is true: app dates
  are local, and these letters would print the PC's zone (po, 2026-10-24, from the PR #39 review).
- Format language? **Decision:** the .NET custom format, because it is built in and well known. Letters that are not
  format letters must be quoted by the user (`'x'`); the panel shows a live example so the user sees the result
  (po, 2026-10-24).
- One-character format? **Decision:** read as a custom format, since .NET would otherwise read `d` as a long date with
  `/`, which gives an invalid name (po, 2026-10-24).
- No date on the item? **Decision:** a per-item error, never a made-up date (`0001-01-01`): the app always supplies dates
  for real files, so this is a safety net (po, 2026-10-24).
- Default separator? **Decision:** `_`, as in Append folder name (NS-28) and the Numbering panel (po, 2026-10-24).
- Which `Validate` format errors exist? **Decision:** only what `DateTime.ToString` refuses; characters that make an
  invalid file name are left to the invalid-name check, like every other rule (po, 2026-10-24).

## Log
- 2026-10-18: created (po).
- 2026-10-24: criteria made testable (enums, culture, no date, validation, edge cases), set Ready (po).
- 2026-10-24: In Progress (builder).
- 2026-10-24: In Review (builder), PR #44.
