# NS-81: Pin the exit code of a refused undo that leaves the history unusable

Status: Ready
Depends on: NS-42
Area: app
Test-only: no code change is expected. If a test fails, the code is fixed to match this story.

## Why
When `undo` retries a revert and every file is refused or rolled back (`RevertOutcome.NothingReverted`) but `pending.json`
cannot be deleted afterwards (`JournalIssue` with phase `AfterFinish`), no file changed, yet the history now reads
Interrupted, so the next `undo` says the history cannot be used (exit 3) and the window's Revert stays off. The NS-42
review asked whether this run should exit 3 or 4. Nothing pins the answer: `ExitCodesTests` has the `AfterFinish` rows for
`apply` (Refused and RolledBack exit 3) but none for a revert.

## Acceptance criteria
1. Given a `RevertResult` of `NothingReverted` (with the skipped list) and a `JournalIssue` of phase `AfterFinish`, then
   `ExitCodes.For` is 3 (`NotRenamed`: nothing changed on disk). A new row `NothingReverted, journal AfterFinish` in
   `ExitCodesTests` (the revert cases and `BuildRevert`; `CmdResults.NothingReverted` gets an `afterFinishIssue` option as
   `Reverted` has).
2. Given the same result through `CommandRunner` with a fake renamer, then the exit code is 3, nothing goes to the output
   stream, and the error stream has the window's `ActionText.ForRevert` text, whose status line ends with the journal-issue
   sentence (as for any `AfterFinish`), so a person reading it learns that the history file could not be cleaned. A test checks
   the exit code and that the text holds the issue's system message.
3. Given the same situation on a real temp history (a `pending.json` the test cannot delete: open with no sharing, or a read-only
   attribute on the folder's file), then the first `undo` exits 3 with that text, and a second `undo` exits 3 with
   `UndoHistoryUnusable` and does not call `Revert()`; both write nothing in the output stream. The test fails with the
   command's output if the scenario cannot be built (it never skips) and releases the file in `Dispose`.
4. Given `PartlyReverted` with an `AfterFinish` issue, then the exit code stays 4 (some files were restored: look at the disk);
   a row pins it.
5. Given the help text of exit code 3 (`Strings.ExitCode3`), then it is unchanged (`Nothing changed (refused, rolled back, no
   history, or a crash before it).`: "no history" already covers the cleaned-up file).

## Out of scope
- Making the history usable again after a failed cleanup (deleting `pending.json` by hand is what the text says today); NS-45.

## Decisions
- Exit 3 or 4 for a refused retry whose `pending.json` stays? **Decision:** 3. The exit code says what happened to the files
  (`apply` already does the same for Refused and RolledBack with the same issue), so a script reads one table; the leftover
  file is told in the text, and the next `undo` says so with its own message and exit 3. Changing it to 4 would make "look at
  the disk" true of a run that touched nothing, and the files to look at are not the ones the user renamed (po, 2026-10-24).
- A code change? **Decision:** none: the code already returns 3; the story only pins it with tests (po, 2026-10-24).

## Log
- 2026-10-24: created and set Ready (po), from the NS-42 review question.
