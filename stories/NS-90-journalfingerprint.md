# NS-90: Undo journal: remember each file's size and date, and check before Revert

Status: Ready
Depends on: NS-9
Area: app
`NameShift.Disk`, `IBatchRenamer` and the one word for a skipped changed file in `ActionText`; the dialog is NS-45.

## Why
So that Revert can tell a file that is still as the batch left it from one that was changed afterwards, and a window or a
script can ask first. This is the engine half of NS-45.

## Acceptance criteria
Tests on net10.0 and net48 against real temp folders (as the NS-9 tests).
1. Given a batch that succeeded, then each file entry of `last-batch.json` also holds `size` (bytes) and `modified` (the
   file's last write time as a UTC `o` string), read from the file at its new path right after the batch. Folder entries
   hold neither. The format version stays 1 (optional members, NS-9 decision). A file whose size or time cannot be read
   (access denied, gone) gets no members, and the batch still succeeds.
2. Given a journal written before this story (no members), then it loads, previews and reverts exactly as today, and none
   of its entries is ever reported as changed.
3. Given `JournaledRenamer.CheckRevert()` (and the same on `IBatchRenamer`), then it reads the journal and the disk and
   returns a `RevertCheck`: the list of entries Revert would skip, each with its `CompletedRename` and a reason
   (`Modified`: the file's size or last write time differs from the journal's; `ItemMissing`; `NameTaken`;
   `WaitsForSkippedItem`: the existing `RevertSkipReason` plus `Modified`). Each entry has one reason, with this
   precedence: `ItemMissing`, then `NameTaken`, then `Modified`, then `WaitsForSkippedItem` (an entry whose old name is
   held by a skipped entry, `Modified` ones included, waits). A test table covers every pair (changed and name taken: `NameTaken`;
   changed and waiting: `Modified`; and so on). It is read-only (no write, move or delete, as
   `LoadLastBatch`) and never throws. When the journal is not revertable (none, reverted, interrupted, unreadable), the list
   is empty.
4. Given a file that was edited (content appended) after the batch, then it is `Modified`; given a file only read, or
   renamed back by hand, then it is not `Modified` (renamed back by hand is `ItemMissing` at the new path). Given a missing
   file that also had a fingerprint, then it is `ItemMissing` only.
5. Given `Revert()`, then it skips `Modified` entries as well (reason `Modified` in the result's skipped list), reverts the
   rest, and keeps the skipped entries in the journal like every other skipped entry, so Revert stays available for them.
6. Given `Revert(includeModified: true)` (new optional parameter, default false), then `Modified` entries are reverted too;
   `ItemMissing` and `NameTaken` are skipped as always and nothing is ever overwritten.
7. Given a chain, a swap and a case-only change (NS-9:7) with fingerprints, then Revert still restores the names in the same
   order, and a folder entry (no fingerprint) is never `Modified`.
8. Given a batch of 10,000 renames, then writing the fingerprints adds one stat per file; the PR writes down the time of the
   batch before and after (not asserted).
9. Given a partial revert, then the entries kept in `last-batch.json` (`JournaledRenamer.Rebase`, also when rebased onto a
    reverted parent folder, and the commit of the revert) keep their `size` and `modified`: a test skips a `Modified` file,
    reverts the others, reads the file back, and `CheckRevert()` still reports the file as `Modified`; a second `Revert()`
    skips it again. The code that rebuilds a `CompletedRename` copies the fingerprint.
10. Given the DTO members `size` and `modified`, then they are written only when set (`EmitDefaultValue = false`): a folder
    entry's JSON holds neither member, not even as null (a test reads the written text).
11. Given `RevertSkipReason.Modified`, then `ActionText` (the one place that words a skip, `SkipLine`) has a case and a
    string for it (`SkipModified`: `{0} was changed after the rename and was not reverted.`), so the window and
    `NameShiftCmd undo` never throw after a revert that skipped a changed file. A test calls `ActionText.ForRevert` with a
    result holding a `Modified` skip, and the existing test that every defined reason has a text still passes. This story is
    safe to merge before NS-45.
12. Given `IFileSystem` gains the read of size and last write time, then it stays read-only (no delete, copy or attribute
   change, as its comment says).

## Out of scope
- The dialog and the command line text (NS-45); hashing file content; fingerprints of folders.

## Decisions
- How is "changed" detected? **Decision:** size and last write time, both exact: cheap (one stat), and a hash of every file
  would make Revert slow for no gain. A change that keeps both is not detected, and that is accepted (po, 2026-10-25).
- Old journals? **Decision:** no members means not checked; no migration, because the journal holds only the last batch
  (po, 2026-10-25).
- Skip by default? **Decision:** yes: `Revert()` never touches a changed file unless asked with `includeModified`, so every
  caller is safe without doing anything (po, 2026-10-25).
- An override at all? **Decision:** yes, one bool for the window's dialog: a skipped changed file would otherwise stay stuck
  in the journal for a user who wants the old name back and keeps the edits (po, 2026-10-25).

- Merge NS-90 and NS-45? **Decision:** no: kept as two PRs, because NS-90 now carries its own text for a skipped changed file
  and is safe alone (po, 2026-10-25, from the PR #90 review).

## Log
- 2026-10-25: created, the engine half of NS-45; set Ready (po).
- 2026-10-25: PR #90 review: the skip text, kept fingerprints on a partial revert, precedence, `ItemMissing` and
  `EmitDefaultValue = false` added (po).
