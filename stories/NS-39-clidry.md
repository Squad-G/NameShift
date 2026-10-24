# NS-39: Command line: quiet and verbose output

Status: Draft
Depends on: NS-38
Area: app

## Why
See what a script would do, and have scripts react to the outcome.

## Acceptance criteria
Already done by NS-38: `--dry-run` itself, conflict refusal, the complete exit codes 0 to 4 and their messages, no prompt.
This story adds `--quiet`, `--verbose` and the documentation of the codes in `--help`.
1. Given `--dry-run`, then it behaves as in NS-38 (kept here as a regression test).
2. Given unchanged files, then they are not printed unless `--verbose` is given.
3. Conflicts and rule errors behave as in NS-38:10 (exit 2, same wording as the window), also with `--quiet` (errors are always printed).
4. The exit codes of NS-38:10 to 12 are listed in `--help` in the same words (a test compares the help text with the table kept in `Strings`).
5. Given `--quiet`, then only errors are printed; errors go to the error stream, results to the output stream.
6. Given any batch size, then the CLI never prompts (scripts never block); the window's 500-file confirmation does not apply to the CLI.

## Out of scope
- Anything not listed above.

## Decisions
- Prompt in the CLI? **Decision:** never, scripts must not hang; `--dry-run` is the safety net (po, 2026-10-18).

## Log
- 2026-10-18: created (po).
