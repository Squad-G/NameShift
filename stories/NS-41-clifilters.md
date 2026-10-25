# NS-41: Command line: filters

Status: In Review
Depends on: NS-38, NS-32, NS-33, NS-70, NS-79, NS-76, NS-93 (build NS-93 first: the new reader uses its helpers)
Area: app

## Why
Pick the files of a folder from a script the way the filter row does in the window: a mask, a pattern, the kinds, hidden
files and a name length. Recursion is its own story (NS-87).

## Acceptance criteria
A new reader `FilterReader` in `ApplyReaders` (NS-79's seam), logic in `NameShift.Cmd`, tests on net10.0 and net48. The
reader builds the window's `FilterPanelModel` (same texts, same error texts) and `CommandRunner` uses its `Filter` where it
builds `new ItemFilter(includeFolders: false)` today. Options (any order, each once):
`--mask <list>`, `--filter-regex <pattern>`, `--filter-ignore-case`, `--min-length <n>`, `--max-length <n>`,
`--include-hidden`, `--files-only`, `--folders-only`, `--include-folders`.
1. Given a folder with `a.jpg`, `b.PNG`, `c.txt` and `--mask "*.jpg;*.png"`, then only `a.jpg` and `b.PNG` are planned
   (case ignored, `;` separates parts, `*` and `?` as in the window); `c.txt` is never touched.
2. Given `--filter-regex "^IMG_[0-9]+$"`, then it is searched in the base name (no extension), as in the window. Given
   `img_1.jpg`, it matches only with `--filter-ignore-case`. Given `--filter-ignore-case` without `--filter-regex`, then
   exit code 1 with the usual "needs option" text.
3. Given `--filter-regex "("` (invalid), then exit code 1, nothing is listed, and the message is `--filter-regex:` followed
   by the window's own regex message (without its "skipped" sentence, as the RegEx rule's option does).
4. Given `--min-length 3 --max-length 5`, then base names of 3 to 5 characters pass (the extension does not count). Given
   `--min-length x`, `-1` or `1.5`, then exit code 1 and `--min-length:` with the window's length text
   (`Strings.FilterLengthInvalid`); the same for `--max-length`. The model reports one joined error for both fields, so
   the reader checks each value on its own (a throwaway `FilterPanelModel` with only that text set) to know which option
   to name. An empty or white-space value is refused by the reader itself with `EmptyOptionText` (as `--date-format`
   does), although the model reads it as "no limit". Given a minimum above the maximum, then exit code 1 with the range
   text, which is the Core constant `ItemFilter.LengthRangeError` (not a `Strings` entry), prefixed `--min-length:`.
5. Given hidden or system files, then they are left out, and `--include-hidden` brings them in (a fake source with
   attributes, and one real temp folder).
6. Given kinds: the default is files only (as NS-38); `--files-only` is accepted and means the same; `--include-folders`
   plans files and folders; `--folders-only` plans folders only. Two of these three together give exit code 1 with
   `Give only one of --files-only, --folders-only or --include-folders.` A real temp folder: `--folders-only` renames a
   folder, the journal records it, and `undo` restores it.
7. Given a mask in the folder argument (`apply D:\Pics\*.jpg ...`), then the folder is `D:\Pics` and the mask is `*.jpg`,
   through the same mask code as `--mask`. Only the last segment may hold `*` or `?`: `D:\Pics\*` works, `apply *.jpg` uses
   the current directory, and `D:\*\x` is still the invalid-folder error. A mask in the path together with `--mask` gives
   exit code 1 with `Give the mask once: in the folder or with --mask.` The path bar's rules (`PathInput`) are not changed.
8. Given filters (or a path mask) that leave no file, then the line `No files match.` goes to the output and the exit code
   is 0; `--quiet` hides it. Without any filter option or mask the behaviour is as today (`Nothing to rename.`).
9. Given `--mask` twice, then `RepeatedOption`; given a missing value (`--mask --dry-run`), then `OptionNeedsValue`; all
   new words are in `CommandLineParser.Words`, so an option word is never taken as a value.
10. Given the filter options and `--preset` or `--save-preset`, then they work together, and a saved preset holds no
    filter (filters are not part of presets, NS-36).
11. Given the apply help (`apply --help`), then it names every new option, with the folder mask.
12. Parity: a test builds the window's `FilterPanelModel` with the same settings for a table of cases (mask, pattern with
    and without ignore case, lengths, hidden, kinds) over one fake listing and checks the CLI keeps exactly the files the
    window keeps.

## Out of scope
- Recursion (NS-87); filters in presets; a filter on size or date; sorting.

## Decisions
- Option names? **Decision:** `--regex` is already the RegEx rule's option (NS-76), so the filter's pattern is
  `--filter-regex` and `--filter-ignore-case` (the rule keeps `--regex-ignore-case`); the other names stay as first planned
  (po, 2026-10-25).
- Default kinds? **Decision:** files only, as NS-38 shipped; `--include-folders` adds folders, because renaming a folder by
  accident from a script is worse than missing one (po, 2026-10-25).
- Build the filter how? **Decision:** through `FilterPanelModel`, so the CLI and the window cannot drift in parsing or in
  error texts (po, 2026-10-25).
- No match: error or success? **Decision:** success with a line (`No files match.`), exit 0: an empty selection is a normal
  answer for a script that runs every night; the line is separate from `Nothing to rename.` so the cause is clear (po,
  2026-10-25).
- Warnings versus errors (for NS-86, NS-87): **Decision:** a warning goes to the error stream and is not hidden by
  `--quiet`, and never changes the exit code (po, 2026-10-25).

## Log
- 2026-10-18: created (po).
- 2026-10-25: re-checked against dev 57d695b (the readers, `FilterPanelModel`, `RunSwitchesReader`): option names fixed (the
  `--regex` clash), recursion split off to NS-87, criteria made testable; set Ready (po).
- 2026-10-25: built on feature/NS-41-clifilters: FilterReader, FolderMask, runner uses the filter; In Review (builder).
