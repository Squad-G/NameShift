# NS-43: Command line: help, version, console text and the exit code of a crash

Status: Ready
Depends on: NS-38
Area: app

## Why
Discover the options without a manual, read file names in any language in the console, and have a script tell "nothing
was touched" from "look at the disk" even when the program fails unexpectedly. First of the CLI follow-ups: it turns
`CommandLineParser` into a dispatcher on the verb and puts the help in place, so NS-42, NS-40 and NS-39 add their lines
to it.

## Acceptance criteria
Code: `src/NameShift.Cmd` (`CommandLineParser`, `CommandRunner`, `Strings.resx`, `ExitCode`) and `src/NameShift.Cmd.Exe`
(`Program`); tests in `tests/NameShift.Cmd.Tests`.

Help and version:
1. Given the first argument is `--help`, `-h`, `/?` or `help`, then the help text is written to the output stream and the
   exit code is 0 (nothing is listed, the disk and the journal are not touched). The text has: a usage line per verb, the
   verbs, every option with one line each, two or three examples, and the exit codes.
2. Given `apply --help` (or `-h`, `/?`) with or without other arguments after it, then the options of `apply` are
   written (the verb's usage, its options and one example) and the exit code is 0, nothing renamed. (`undo --help`
   is added by NS-42.)
3. Given `--version`, then `NameShift <version>` is written to the output stream, exit 0. `<version>` is the exe's
   `AssemblyInformationalVersion` without a `+build` suffix; `CommandRunner` takes it as a constructor parameter (tests
   give `1.2.3`), `Program` passes the real one. A test fails when the `<Version>` of `NameShift.Cmd.Exe.csproj` and
   `NameShift.App.csproj` differ (both files found from `NameShift.slnx`; fails, not skips, when not found).
4. Given a usage error (unknown verb or option, a missing value, no folder, no rule), then the error stream gets one
   line: the message, the usage line (`Strings.Usage`), then `For help, run: NameShiftCmd --help` (a new string), exit 1.
   The messages themselves are NS-38's, unchanged.
5. Given no arguments, then it is a usage error as in criterion 4 (exit 1, message `Strings.NoCommand`), not help: a
   script that lost its arguments must fail, not succeed.
6. Given the help text (all of it, every verb), then every line is at most 79 characters and ASCII only, and a test
   compares it with the parser: every option word the parser accepts appears in the help text (a new option without
   help fails this test, which is how NS-42, NS-40 and NS-39 stay honest).
7. Given the exit codes, then the help lists each `ExitCode` value with its meaning in the same words as `ExitCode`'s
   summaries and `Strings` (a test walks `Enum.GetValues(typeof(ExitCode))` and finds `<number>` and its text in the help),
   with the wording: 0 done or nothing to do; 1 usage or input error; 2 conflicts or rule errors, nothing renamed;
   3 nothing renamed (refused, rolled back, history not saved, or an unexpected error before any file was touched);
   4 look at the disk (items left under other names, or history not saved after the rename, or an unexpected error
   during the rename).

An unexpected error (split out of the NS-38 review):
8. Given an exception raised before the renamer is called (the listing, the planner), then nothing was renamed for
   certain: the error stream gets `NameShift hit an unexpected problem. No file was renamed.` (a new string) and the exit
   code is 3. Given an exception raised by `IBatchRenamer.Rename` (or by writing the result after it), then the disk
   state is unknown: the error stream gets the window's `Strings.ActionUnexpected` and the exit code stays 4. A test
   for each, with a fake `IFileListSource` that throws and a fake `IBatchRenamer` that throws.
9. Given the exception text, then it is never printed (no stack trace, no path): the line is the string only.

Console text:
10. Given a file name with characters outside the console's code page (`é`, `日本語.txt`), then NameShiftCmd writes its
    output and errors as UTF-8 without a BOM: `Program.Main` sets `Console.OutputEncoding` to UTF-8 (the setter's
    default writer must not emit a BOM when the output is redirected) and restores the previous encoding in a `finally`.
    A net48 test starts the built exe with redirected output, a temp folder holding `é.TXT` and `日本語.TXT`,
    `apply <folder> --case lower --dry-run`, and reads the raw output bytes: they decode as UTF-8 to
    `é.TXT -> é.txt` and `日本語.TXT -> 日本語.txt`, with no `EF BB BF` first.
11. Given the library (`CommandRunner`), then it still takes and writes `TextWriter`s and knows nothing of encodings
    (the encoding is the exe's job); no input is ever read.
12. By hand, written in the PR: in cmd (`chcp 437` and `chcp 65001`), in Windows PowerShell 5.1 and in PowerShell 7,
    run the dry run of criterion 10 on a scratch folder: the names are readable on screen in all four. When the
    output is captured by PowerShell 5.1 (`$x = & NameShiftCmd ...`) it decodes with its own code page, so non-ASCII
    names may show `?`: this is a known limit (PowerShell 5.1 reads native output with the OEM code page), listed in the
    PR, not a defect to fix here.

## Out of scope
- `--quiet` and `--verbose` (NS-39), `undo` (NS-42), more rule options (NS-40), a man page or website docs.
- Localised help or localised messages (the strings are English, in `Strings.resx` for later).
- A `--encoding` option.

## Decisions
- No arguments: help or an error? **Decision:** an error (exit 1) that points to `--help`: a script that lost its
  arguments must fail loudly, and `--help` is one word away. This replaces the first version of this story, which printed
  usage and exited 0 (po, 2026-10-24).
- Exit 3 or 4 for an exception before any rename? **Decision:** 3 when the renamer was never called (nothing can have
  been renamed), 4 when it was (the disk state is unknown). No new code number: the five codes stay, so scripts that
  treat 3 as "retry or inspect, nothing changed" and 4 as "look" stay right (po, 2026-10-24, from the NS-38 review).
- Console encoding? **Decision:** UTF-8 without BOM, set by the exe and restored on exit. The default OEM code page turns
  non-Latin names into `?`, and the output is part of what a script reads. Restoring the encoding avoids leaving the
  user's console changed after the run. A file name that cannot be shown is a worse bug for a renamer than the
  PowerShell 5.1 capture limit of criterion 12 (po, 2026-10-24).
- Why not merge into NS-39? **Decision:** kept apart: help, version and the exit table are discovery and need no output
  modes; NS-39 is only the output volume (po, 2026-10-24).

## Log
- 2026-10-18: created (po).
- 2026-10-24: rewritten on top of the merged NS-38: no-argument behaviour, exit code of a crash, UTF-8 console, a help
  test tied to the parser; set Ready (po).
