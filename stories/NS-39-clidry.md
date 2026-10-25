# NS-39: Command line: quiet and verbose output

Status: Done
Depends on: NS-38, NS-43, NS-42
Area: app

## Why
Scripts want silence on success and a log when something goes wrong; people at a prompt want to see every file.
(`--dry-run`, conflicts, the exit codes and "never prompt" are NS-38's; the exit table in `--help` is NS-43's.) NS-42 is
listed only for the build order: both change the parser, the runner and the help, and NS-42 rewrites the usage lines.

## Acceptance criteria
Code: `src/NameShift.Cmd` (`CommandLineParser`, the apply options, `CommandRunner`, `Strings.resx`); the usage lines are
generic since NS-42 and are not touched here. Output today (NS-38): a real run prints the window's summary
(`2 files renamed.`) on the output stream; `--dry-run` prints `old -> new` for each changed file; nothing to rename prints
`Nothing to rename.`; everything not exit 0 goes to the error stream. Unchanged files are never printed today.
1. Given the new options `--quiet` (`-q`) and `--verbose` (`-v`), then both are parsed on `apply` in any position and
   are listed in `apply --help` and in the main help on their own lines (`  --quiet, -q ...`, `  --verbose, -v ...`,
   one sentence each, within 79 columns), and the four words are added to `CommandLineParser.ApplyOptionWords` (so
   `Words` and the option-word guard see them; NS-43's `EveryWord_IsInTheHelp` fails otherwise). A repeated one, or one
   given in both spellings (`-q --quiet`), is the usual `Strings.RepeatedOption` usage error naming the long form
   (`--quiet can be given only once.`). `undo` does not take them (`undo -q` is NS-42's usage error).
   `CommandLineParserTests.Words_AreTheDispatchedWords` (which pins the exact list) is updated to the new list.
2. Given `--quiet` with `--verbose`, or `--quiet` with `--dry-run`, then it is a usage error (exit 1, nothing listed or
   renamed, one line in the NS-43:4 shape: message, `Strings.Usage`, `Strings.HelpPointer`). The two messages are new
   strings: `QuietWithVerbose` = `--quiet and --verbose cannot be used together.` and `QuietWithDryRun` = `--quiet and
   --dry-run cannot be used together.` (a dry run that prints nothing has no use). When both conflicts are present,
   `QuietWithVerbose` wins.
3. Given `--quiet` and a successful batch (exit 0), then nothing is written to either stream; `Nothing to rename.` is
   not written either. Given `--quiet` and a failure (exit 1, 2, 3 or 4), then the errors are written to the error
   stream exactly as without `--quiet` (every conflict, rule error, refusal, stranded item and history warning, the
   folder and input errors, the unexpected-problem lines). A test per exit code.
4. Given `--verbose` and a real run, then the output stream gets, in this order: each renamed file as `old -> new` (file
   names, list order, the same line format as the dry run, `Strings.DryRunLine`), then each file of the list whose
   `RenameResult.Status` is `Unchanged` as `<name>: unchanged` (new string `UnchangedLine` = `{0}: unchanged`), then the
   summary as today.
5. Given `--verbose` and `--dry-run`, then the changed files are listed as today, then the `<name>: unchanged` lines of
   criterion 4 (and no summary, as today). Files the CLI never renames (hidden, system, folders) are not listed in any
   mode.
6. Given `--verbose` and nothing to rename (every file `Unchanged`), then the `<name>: unchanged` lines come first and
   `Nothing to rename.` after them, exit 0.
7. Given no verbosity option, then the output is byte for byte what NS-38 wrote (a regression test on a dry run, a real
   run, a conflict, and `Nothing to rename.`).
8. Given `--verbose` and a batch that fails (exit 2, 3 or 4), then the errors are as without it: `--verbose` adds lines
   only on success.
9. Given the option-word guard, then `-q` and `-v` count as option words like `-h` today: `--replace IMG -v` is the
   `ReplaceNeedsValues` error and `--case -q` is `BadCase`/`CaseNeedsValue` as for any option word; a replacement text
   that is exactly `-q` or `-v` cannot be given (a test pins this, and the PR says so).
10. Given any mode, then nothing is ever read from the input stream and no prompt appears (a test runs with an input
    stream that throws on read; the window's 500-file confirmation does not apply).

## Out of scope
- A log file option, colour, progress output, machine-readable output (JSON). Revisit when a script user asks.
- `undo` output (it does not take `--quiet` or `--verbose`; revisit if asked).
- The `Preset "NAME" saved.` line of NS-69 (that story says how `--quiet` and `--verbose` treat it).

## Decisions
- Prompt in the CLI? **Decision:** never, scripts must not hang; `--dry-run` is the safety net (po, 2026-10-18).
- `--quiet` with `--dry-run`? **Decision:** refuse: the dry run exists to be read, so combining them is a mistake the
  user should hear about, not a silent no-op (po, 2026-10-24).
- Do errors survive `--quiet`? **Decision:** yes, always: quiet hides success, never failure, and the exit code alone
  cannot say which file (po, 2026-10-24).
- Short forms? **Decision:** `-q` and `-v` only; no other short options, to keep the syntax small (po, 2026-10-24).
- Split from the first version: unchanged-file listing now needs `--verbose` as a dry-run extra instead of an extra
  line in every run, because default output must not change under existing scripts (po, 2026-10-24).
- `-q` and `-v` as option words? **Decision:** yes, the guard reads the one word list, so a missing value is caught as
  for `-h`; the price is that `--replace x -v` cannot mean "replace x with -v". Safe over clever: the old behaviour
  (rename for real with a surprising text) is the failure to avoid (po, 2026-10-24).
- Two messages for the two conflicts? **Decision:** yes, plain and specific; one generic "options conflict" line would
  not say which pair (po, 2026-10-24).

## Log
- 2026-10-18: created (po).
- 2026-10-24: rewritten after NS-38 merged: exit codes and help moved to NS-38 and NS-43; concrete output rules (po).
- 2026-10-24: NS-43 merged. Re-checked against its code: words go in `ApplyOptionWords`, the two conflict messages and
  the unchanged line chosen, the verbose nothing-to-rename case, the `-q`/`-v` guard effect and repeated aliases added;
  depends on NS-42 for the build order only; set Ready, second of NS-42, NS-39, NS-40, NS-69 (po).
- 2026-10-24: In Progress (builder).
- 2026-10-24: In Review (builder), PR #73.
- 2026-10-25: Done, PR #73 squash-merged into `dev` after the reviewer's APPROVE; review nits folded in (doc comment re-wrapped, a `--quiet --dry-run` usage row without a folder, a verbose test pinning that the lines come from the plan) (tech-lead).
