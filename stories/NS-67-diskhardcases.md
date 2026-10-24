# NS-67: Hard cases on disk: read-only, in use, vanishing drive

Status: In Progress
Depends on: NS-9, NS-15
Area: app

## Why
A batch must be safe when a file is read-only, locked by another program, or its drive disappears, and the message must
say what happened. (Split from NS-48.)

## Acceptance criteria
Tests are in `NameShift.Disk.Tests` and `NameShift.Ui.Tests` (net10.0 and net48), against real temp folders unless it says
otherwise; no admin rights are needed or requested. Fault tests use `HookedFileSystem` (its `FailMove` hook, which throws
the given exception on a chosen move while the others really run in a temp folder), not `FakeFileSystem` (whose `Move` is
not supported).
1. Given a read-only file `a.txt` renamed to `b.txt` through `JournaledRenamer`, then `b.txt` exists, `a.txt` does not, `b.txt`
   is still read-only, and the journal has the entry; Revert restores `a.txt`, still read-only. Same for a read-only folder.
2. Given a file open in another program (opened with `FileShare.None`), when a batch renames it, then the batch fails and is
   rolled back (`ExecutionOutcome.RolledBack`), every earlier rename of the batch is undone, no journal entry is written,
   and the failure is a new `FailureReason.ItemInUse` carrying the item's path. `ItemInUse` is set when the rename call
   fails with a Windows sharing violation (HResult low word 32) or lock violation (33); `ExecutionFailure` keeps the system
   message and HResult as for `RenameFailed`, and its doc comment (which says "NS-48 uses it") is corrected to NS-67.
3. Given `FailureReason.ItemInUse`, then `ActionText.FailureSentence` is built from a new string `ItemInUse` in
   `Strings.resx` with value `{0} is in use by another program: close it and try again.` (`{0}` = the file name only); the
   test asserts the Rename status text equals `Nothing was renamed. ` + that sentence (the `NothingRenamed` string and a
   space, as `ActionText` joins them today). Any other `RenameFailed` keeps its text.
4. Given a 3-item batch where the second file is locked, then the first file has its old name again afterwards and the third
   was never touched (checked on disk).
5. Given `HookedFileSystem` with `FailMove` throwing an `IOException` with HResult 0x80070003 (drive or share gone) on the
   second move only, then the outcome is `RolledBack` (the undo moves run for real), with no journal entry.
6. Given `FailMove` throwing that exception on the second move and on every undo move, then the outcome is
   `NeedsAttention`, the stranded items are listed with their current and original paths, the journal reads `Interrupted`
   and Revert is disabled (NS-9), and nothing is deleted.
7. Given a batch of one changed item and unchanged items with names of 200 or more characters, then the executor makes no
   move for the unchanged ones (`HookedFileSystem.Moves` has only the changed item) and the journal lists only the changed
   item. The unchanged items need not exist on disk (a file name that long cannot be created on .NET Framework 4.8): the
   results are built with their validating constructors and the file system is the hooked one.
8. Given Revert hits a locked file (the existing `PartialRevertTests.Revert_FileLockedDuringRevert`), then it is skipped and
   listed as before. Because the new reason can now come out of the executor, `JournaledRenamer.SkipFailedEntry` maps
   `FailureReason.ItemInUse` to `RevertSkipReason.RenameFailed` with the system message and HResult (like `RenameFailed`),
   instead of throwing `InvalidOperationException` in its `default` case; a test locks a file during Revert and expects the
   skipped entry, not an exception. The Revert wording stays as it is today (not part of this story).

## Out of scope
- Names that differ only by Unicode form: NS-48.
- Retrying automatically or waiting for the lock to end; closing the other program for the user.
- Finding out which program holds the file.
- Wording for locked files in Revert (a later story if needed).

## Decisions
- Read-only files? **Decision:** rename them and keep the attribute: renaming does not change the content, and the user
  asked for it by selecting the file (po, 2026-10-24).
- Retry on "in use"? **Decision:** no, stop and roll back at once, because the batch is all or nothing and a silent wait
  could hang a script (po, 2026-10-24).
- Elevation? **Decision:** never asked for; an access denied error shows its system message as now (po, 2026-10-24).
- Revert and a locked file? **Decision:** same skip as any failed rename, so a new failure reason can never crash Revert (po,
  2026-10-24, from the PR #53 review).

## Log
- 2026-10-24: split out of NS-48; set Ready (po).
- 2026-10-24: after review: `SkipFailedEntry` mapping, `HookedFileSystem`, `{0}` string and full status text, unchanged items
  need not exist (po).
- 2026-10-24: In Progress (builder).
