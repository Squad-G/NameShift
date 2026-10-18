# NS-43: Command line: help and version

Status: Draft
Depends on: NS-38
Area: app

## Why
Discover the options without a manual.

## Acceptance criteria
1. Given `--help`, `-h` or `/?`, then usage, verbs, options, examples and the exit codes are printed and the exit code is 0. Starting the app with no arguments is not help: it opens the window (NS-10, NS-47); if the tech lead builds a separate console program, that program with no arguments prints usage and exits 0.
2. Given `--version`, then `NameShift <version>` is printed.
3. Given `NameShift apply --help`, then the options of that verb are listed.
4. Given a usage error, then the message plus a pointer to `--help` goes to the error stream, exit 1.
5. Help text is written in our own words and fits in 80 columns.

## Out of scope
- Anything not listed above.

## Decisions
- None needed.

## Log
- 2026-10-18: created (po).
