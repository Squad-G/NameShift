# NS-67: Hard cases on disk: read-only, in use, vanishing drive

Status: Ready
Depends on: NS-9, NS-15
Area: app

## Why
A batch must be safe when a file is read-only, locked by another program, or its drive disappears, and the message must
say what happened. (Split from NS-48.)

## Acceptance criteria
Tests are against real temp folders (`NameShift.Disk.Tests`, net10.0 and net48) unless it says otherwise; no admin rights
are needed or requested.
1. Given a read-only file `a.txt` renamed to `b.txt` through `JournaledRenamer`, then `b.txt` exists, `a.txt` does not, `b.txt`
   is still read-only, and the journal has the entry; Revert restores `a.txt`, still read-only. Same for a read-only folder.
2. Given a file open in another program (opened with `FileShare.None`), when a batch renames it, then the batch fails and is
   rolled back (`ExecutionOutcome.RolledBack`), every earlier rename of the batch is undone, no journal entry is written,
   and the failure is `FailureReason.ItemInUse` carrying the item's path. (`ItemInUse` is a new reason, set when the rename
   call fails with a Windows sharing violation (HResult low word 32) or lock violation (33).)
3. Given `FailureReason.ItemInUse`, then `ActionText` says exactly `{name} is in use by another program: close it and try
   again.` (a new string in `Strings.resx`; the name is the file name only); any other `RenameFailed` keeps its text.
4. Given a 3-item batch where the second file is locked, then the first file has its old name again afterwards and the third
   was never touched (checked on disk).
5. Given a fake file system (the existing test fake) that throws a not-found/IO error (HResult 0x80070003, drive or share
   gone) on the second rename, then the outcome is `RolledBack` when the undo steps succeed, with no journal entry.
6. Given the same fake also failing every undo step (the drive is still gone), then the outcome is `NeedsAttention`, the
   stranded items are listed with their current and original paths, the journal reads `Interrupted` and Revert is
   disabled (NS-9), and nothing is deleted.
7. Given a changed item among unchanged items with names of 200 or more characters, then the executor makes no rename call
   for the unchanged ones (the fake counts the calls) and the journal lists only the changed item.
8. Given Revert hits a locked file, then it is skipped and listed as before (NS-9:8); its wording stays as it is today
   (not part of this story).

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

## Log
- 2026-10-24: split out of NS-48; set Ready (po).
