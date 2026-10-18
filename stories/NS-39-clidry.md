# NS-39: Command line: dry run, conflicts and exit codes

Status: Draft
Depends on: NS-38
Area: app

## Why
See what a script would do, and have scripts react to the outcome.

## Acceptance criteria
1. Given `--dry-run`, then lines `old -> new` are printed for every changed file and the disk and the journal are not touched.
2. Given unchanged files, then they are not printed unless `--verbose` is given.
3. Given any conflict (duplicate target, existing target, invalid or reserved name, path too long), then nothing is renamed, each conflict is printed with the same plain wording as the window, and the exit code is 2; a dry run reports the same conflicts and also exits 2.
4. Exit codes are documented in `--help` and fixed: 0 success (including nothing to do), 1 usage or input error (bad option, missing folder or preset), 2 conflicts found, 3 rename failed (batch rolled back), 4 rollback incomplete (needs attention).
5. Given `--quiet`, then only errors are printed; errors go to the error stream, results to the output stream.
6. Given `--yes` is absent and a batch exceeds 500 files, then it still does not prompt (scripts never block); the window's confirmation does not apply to the CLI.

## Out of scope
- Anything not listed above.

## Decisions
- Prompt in the CLI? **Decision:** never, scripts must not hang; `--dry-run` is the safety net (po, 2026-10-18).

## Log
- 2026-10-18: created (po).
