# NS-43: Command line: help, version, console text and the exit code of a crash

Status: In Progress
Depends on: NS-38
Area: app

## Why
Discover the options without a manual, read file names in any language in the console, and have a script tell "nothing
was touched" from "look at the disk" even when the program fails unexpectedly. First of the CLI follow-ups: it turns
`CommandLineParser` into a dispatcher on the verb and puts the help in place, so NS-42, NS-40 and NS-39 add their lines
to it.

## Acceptance criteria
Code: `src/NameShift.Cmd` (`CommandLineParser`, `CommandRunner`, `Strings.resx`, `ExitCode`) and `src/NameShift.Cmd.Exe`
(`Program`); tests in `tests/NameShift.Cmd.Tests`. No file under `tools/` or `docs/` changes (the release scripts are
David's).

Help and version:
1. Given the first argument is `--help`, `-h`, `/?` or `help`, then the help text is written to the output stream and the
   exit code is 0 (nothing is listed, the disk and the journal are not touched); any arguments after it are ignored
   (`help apply` and `--help x` print the same main help). The text has: a usage line per verb, the verbs, every option
   with one line each, two or three examples, and the exit codes.
2. Given `apply` followed anywhere by `--help`, `-h` or `/?` (`apply --help`, `apply C:\x --help`), then the options of
   `apply` are written (its usage, its options, one example), exit 0, nothing listed or renamed, even when the other
   arguments are wrong. (`undo --help` is added by NS-42.)
3. Given `--version` as the first argument (anything after it is ignored), then `NameShift <version>` is written to the
   output stream, exit 0. `<version>` is the exe's own `AssemblyInformationalVersion` without a `+build` suffix
   (whatever the csproj gives; today the SDK default `1.0.0`); `CommandRunner` takes it as a parameter of its
   constructor and of `ForDisk` (tests give `1.2.3`; `Program.cs` keeps `ForDisk` and `JournaledRenamer.DefaultHistoryRoot`).
   A net48 exe test starts `NameShiftCmd.exe --version` and compares the line with the exe's own
   `FileVersionInfo.ProductVersion` (build suffix removed). No version test touches the App's csproj. Until David
   answers the open release-process question (how the versions are set), `--version` prints `1.0.0` while the window
   app is `0.1.0`; the build PR says so, so nobody reads `1.0.0` as a release.
4. Given a usage error (unknown verb or option, a missing value, no folder, no rule, none of them help), then exactly
   one line goes to the error stream: `<message> <usage> For help, run: NameShiftCmd --help`, the three parts joined by
   single spaces, where `<usage>` is `Strings.Usage` (it stays the one-line form, it is not limited to 79 columns) and
   the pointer is a new string `Strings.HelpPointer`. Exit 1. The messages are NS-38's, unchanged. Merged tests that
   change: `ConsoleExeTests.Exe_UsageError_WritesToStderr_ExitCode1` (now expects `Strings.NoFolder + " " + Strings.Usage +
   " " + Strings.HelpPointer`) and the parser and runner tests that compare `message + " " + Strings.Usage`; `apply`
   with a quote in the folder keeps its own message and exit 1.
5. Given no arguments, then it is a usage error as in criterion 4 (exit 1, message `Strings.NoCommand`), not help: a
   script that lost its arguments must fail, not succeed.
6. Given the help (`Strings` entries `HelpMain` and `HelpApply`, each a multi-line text), then every line is at most 79
   characters and ASCII only (a test checks every line of both). The usage lines inside the help are their own strings
   (`HelpUsageApply`, wrapped over several lines), not `Strings.Usage`.
7. Given the parser, then it exposes the words it dispatches on as one list, `CommandLineParser.Words` (internal, with
   `InternalsVisibleTo("NameShift.Cmd.Tests")`): the verbs, every option and its short form, and the help and version
   words including `/?`; the `--case` values are not in it (the help lists them in the `--case` line, checked once by
   hand). The dispatch and the option-word guard (`IsOptionWord`) read this same list, so a new option cannot be parsed
   without being in it. A test fails when a word of the list is missing from the help text (`HelpMain` for verbs and
   global words, `HelpApply` for `apply` options).
8. Given the exit codes, then each code has its own entry, `Strings.ExitCode0` to `Strings.ExitCode4` (the one-line
   meaning), the help prints them as `<number>  <text>`, and a test walks `Enum.GetValues(typeof(ExitCode))` and
   finds each number and its `Strings` text in the help (a code with no entry fails). The `ExitCode` summaries are
   reworded to the same meaning: 0 done or nothing to do; 1 usage or input error; 2 conflicts or rule errors, nothing
   renamed; 3 nothing renamed (refused, rolled back, history not saved, or an unexpected error before any file was
   touched); 4 look at the disk (items left under other names, history not saved after the rename, or an unexpected
   error during the rename).

An unexpected error (split out of the NS-38 review):
9. Given an exception raised before the renamer is called (the listing, the planner), then nothing was renamed for
   certain: the error stream gets `NameShift hit an unexpected problem. No file was renamed.` (a new string,
   `Strings.UnexpectedBeforeRename`) and the exit code is 3. Given an exception raised by `IBatchRenamer.Rename` or while
   writing its result, the disk state is unknown: the error stream gets the window's `Strings.ActionUnexpected` and the
   exit code stays 4. A test for each, with a fake `IFileListSource` that throws and a fake `IBatchRenamer` that
   throws. Merged tests that change: `CommandRunnerTests.UnexpectedException_Exit4_SaysSomethingWentWrong` (a listing that
   throws: now exit 3 and `Strings.UnexpectedBeforeRename`) and `BothStreamsFail_StillExit4` (a listing that throws and
   a failing error stream: now exit 3); a test for exit 4 with a throwing `Rename` is added (a failing error stream there
   still gives 4).
10. Given the exception text, then it is never printed (no stack trace, no path): the line is the string only.

Console text:
11. Given a file name with characters outside the console's code page (`é`, `日本語.txt`), then NameShiftCmd writes its
    output and errors as UTF-8 without a BOM (`new UTF8Encoding(false)`). `Program.Main` sets `Console.OutputEncoding`
    first, before anything reads `Console.Out` or `Console.Error` (a writer taken earlier keeps the old code page and
    writes `?`), and only then passes the writers to `ForDisk`. The previous encoding is restored in a `finally`.
12. Given there is no console attached (a detached process: the setter and the restore throw `IOException: The handle is
    invalid`), then setting or restoring the encoding is wrapped in a `try/catch` that ignores `IOException` (and
    only that), the run goes on and the exit code is still 0 to 4. The console is behind a small seam in `NameShift.Cmd`
    (not in the exe, which tests cannot reference): an internal `IConsoleEncoding` with a get and a set, its real
    implementation over `System.Console`, and an internal entry method that takes it and that `Program.Main` calls
    (visible to `NameShift.Cmd.Tests`, as in criterion 7). A unit test with a fake that throws checks that a throwing
    set and a throwing restore are both ignored and the command still returns its code.
13. Given a net48 test starts the built exe on a temp folder holding `é.TXT` and `日本語.TXT` with
    `apply <folder> --case lower --dry-run`, then for output redirected to a pipe and (a second test) to a file
    (`cmd /c "... > file"`) the raw bytes decode as UTF-8 to `é.TXT -> é.txt` and `日本語.TXT -> 日本語.txt`, and the first
    bytes are not `EF BB BF`. The library (`CommandRunner`) still takes and writes `TextWriter`s, knows nothing of
    encodings and never reads input.
14. By hand, written in the PR: in cmd (`chcp 437` and `chcp 65001`), in Windows PowerShell 5.1 and in PowerShell 7,
    run the dry run of criterion 13 on a scratch folder: the names are readable on screen in all four. When the
    output is captured by PowerShell 5.1 (`$x = & NameShiftCmd ...`), it decodes the UTF-8 bytes with its own OEM code
    page, so non-ASCII names can show as garbled letters (mojibake): a known limit, listed in the PR, not a defect to
    fix here.

## Out of scope
- `--quiet` and `--verbose` (NS-39), `undo` (NS-42), more rule options (NS-40), a man page or website docs.
- Localised help or localised messages (the strings are English, in `Strings.resx` for later).
- A `--encoding` option.
- Stamping `NameShiftCmd.exe` with the same version as `NameShift.exe`: the release scripts (`tools/bump-version.sh`,
  `tools/release.sh`) and `docs/RELEASING.md` are David's. Recorded as a release-process question for him (the lead
  puts it in QUESTIONS.md); until he decides, `--version` shows the exe's own number.

## Decisions
- No arguments: help or an error? **Decision:** an error (exit 1) that points to `--help`: a script that lost its
  arguments must fail loudly, and `--help` is one word away. This replaces the first version of this story, which printed
  usage and exited 0 (po, 2026-10-24).
- Exit 3 or 4 for an exception before any rename? **Decision:** 3 when the renamer was never called (nothing can have
  been renamed), 4 when it was (the disk state is unknown). No new code number: the five codes stay, so scripts that
  treat 3 as "retry or inspect, nothing changed" and 4 as "look" stay right (po, 2026-10-24, from the NS-38 review).
- Console encoding? **Decision:** UTF-8 without BOM, set by the exe before any writer is taken, restored on exit, and
  never fatal when there is no console. The default OEM code page turns non-Latin names into `?`, and the output is
  part of what a script reads (po, 2026-10-24).
- Why not merge into NS-39? **Decision:** kept apart: help, version and the exit table are discovery and need no output
  modes; NS-39 is only the output volume (po, 2026-10-24).
- `--version` and the App's version? **Decision:** independent for now: the story must not touch David's release
  scripts; whether the two exes share one stamped version is his release-process question (po, 2026-10-24, from the PR
  #63 review).
- Help after other arguments? **Decision:** `help` and `--help` as the first argument win over anything after them;
  `-h`, `--help` and `/?` anywhere after `apply` win over the other arguments, so a user who typed a half command and
  then `--help` gets help, not an error (po, 2026-10-24).

## Log
- 2026-10-18: created (po).
- 2026-10-24: rewritten on top of the merged NS-38: no-argument behaviour, exit code of a crash, UTF-8 console, a help
  test tied to the parser; set Ready (po).
- 2026-10-24: after the PR #63 review: no version-match test (release scripts are David's), encoding order and
  no-console tolerance, usage versus help lines, data sources for the help tests, merged tests listed (po).
- 2026-10-24: In Progress (builder).
