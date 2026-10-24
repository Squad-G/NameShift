# NS-39: Command line: quiet and verbose output

Status: Draft
Depends on: NS-38, NS-43
Area: app

## Why
Scripts want silence on success and a log when something goes wrong; people at a prompt want to see every file.
(`--dry-run`, conflicts, the exit codes and "never prompt" are NS-38's; the exit table in `--help` is NS-43's.) Draft
only until NS-43 is merged (the parser and the help test it adds); the criteria are final.

## Acceptance criteria
Output today (NS-38): a real run prints the window's summary (`2 files renamed.`) on the output stream; `--dry-run` prints
`old -> new` for each changed file; nothing to rename prints `Nothing to rename.`; everything not exit 0 goes to the
error stream. Unchanged files are never printed today.
1. Given the new options `--quiet` (`-q`) and `--verbose` (`-v`), then both are parsed on `apply` in any position, are
   listed in `apply --help` and in the main help (NS-43's help test fails otherwise), and a repeated one is the
   usual "can be given only once" usage error.
2. Given `--quiet` with `--verbose`, or `--quiet` with `--dry-run`, then it is a usage error (exit 1, nothing listed or
   renamed, one line saying the two cannot be combined): a dry run that prints nothing has no use.
3. Given `--quiet` and a successful batch (exit 0), then nothing is written to either stream; `Nothing to rename.` is
   not written either. Given `--quiet` and a failure (exit 1, 2, 3 or 4), then the errors are written to the error
   stream exactly as without `--quiet` (every conflict, rule error, refusal, stranded item and history warning).
4. Given `--verbose` and a real run, then the output stream gets, in this order: each renamed file as `old -> new` (file
   names, list order, the same line format as the dry run), then each file of the list whose name stays the same as
   `<name>: unchanged` (a new string), then the summary as today.
5. Given `--verbose` and `--dry-run`, then the changed files are listed as today, then the `<name>: unchanged` lines of
   criterion 4 (and no summary, as today). Files the CLI never renames (hidden, system, folders) are not listed in
   any mode.
6. Given no verbosity option, then the output is byte for byte what NS-38 wrote (a regression test on a dry run, a real
   run, a conflict, and `Nothing to rename.`).
7. Given `--verbose` and a batch that fails (exit 2, 3 or 4), then the errors are as without it: `--verbose` adds lines
   only on success.
8. Given any mode, then nothing is ever read from the input stream and no prompt appears (a test runs with an input
   stream that throws on read; the window's 500-file confirmation does not apply).

## Out of scope
- A log file option, colour, progress output, machine-readable output (JSON). Revisit when a script user asks.
- `undo` output (NS-42 uses the same two options only if asked later).

## Decisions
- Prompt in the CLI? **Decision:** never, scripts must not hang; `--dry-run` is the safety net (po, 2026-10-18).
- `--quiet` with `--dry-run`? **Decision:** refuse: the dry run exists to be read, so combining them is a mistake the
  user should hear about, not a silent no-op (po, 2026-10-24).
- Do errors survive `--quiet`? **Decision:** yes, always: quiet hides success, never failure, and the exit code alone
  cannot say which file (po, 2026-10-24).
- Short forms? **Decision:** `-q` and `-v` only; no other short options, to keep the syntax small (po, 2026-10-24).
- Split from the first version: unchanged-file listing now needs `--verbose` as a dry-run extra instead of an extra
  line in every run, because default output must not change under existing scripts (po, 2026-10-24).

## Log
- 2026-10-18: created (po).
- 2026-10-24: rewritten after NS-38 merged: exit codes and help moved to NS-38 and NS-43; concrete output rules; Draft
  until NS-43 is merged (po).
