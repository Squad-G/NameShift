# NS-9: Undo journal and Revert

Status: Done
Depends on: NS-8
Area: app

## Why
Every batch is logged so it can be undone, even after the app is closed.

## Acceptance criteria
1. Given a successful batch of 3 renames, then a journal entry with time, folder(s) and the old/new full paths of all 3 is stored under `%LOCALAPPDATA%\NameShift\history`.
2. Given an entry exists, when Revert runs, then every file gets its old name back, and afterwards Revert is disabled until a new batch is made (one level of undo; the reverted entry is not undone again and does not expose the batch before it).
3. Given the app was restarted after the batch, then the last batch can still be reverted.
4. Given a failed (rolled back) batch, then no journal entry is written.
5. Given the journal file is corrupt or unreadable, then the app starts normally, Revert is disabled, and the corrupt file is kept untouched.
6. Given a new batch succeeds, then the journal keeps only that batch (it replaces the previous entry), because only the last batch can be reverted.
7. Given the batch contains a chain (`a`->`b`, `b`->`c`), a swap (`a`<->`b`) or a case-only change (`photo.jpg` -> `Photo.jpg`), then Revert restores the old names using the same ordering and temporary-name mechanism as the executor (NS-8), verified in a temp folder for each of the three.
8. Given Revert fails on one file (it was removed or a file now has the old name), then nothing is overwritten; the result lists the files that could not be reverted and the entry stays available for those.

## Out of scope
- Revert safety warnings (NS-45).
- Reverting older batches than the last (not planned: one level of undo is the product).

## Decisions
- Revert only the last batch? **Decision:** yes for now, because one level matches the plan and is the simplest safe behaviour (po, 2026-10-18).
- Keep 50 entries? **Decision:** no, keep only the last batch, because older entries could never be used and would only pile up personal paths on disk (po, 2026-10-18).
- Is the journal a privacy issue? **Decision:** it stays on the PC and is never sent anywhere; it holds only paths the user renamed (po, 2026-10-18).
- **Placement:** all the code is in `src/NameShift.Disk` (disk work); `NameShift.Core` is untouched and there is no UI change (the buttons are NS-15). `JournaledRenamer` is the app's only entry point to run a batch, so a batch never runs without its journal; `RenameExecutor` stays public and unchanged (tech-lead, 2026-10-18).
- **Format:** JSON through the in-box `DataContractJsonSerializer` (no NuGet package, no System.Text.Json), UTF-8 without BOM, indented; times are UTC "o" strings, kinds are `"file"` / `"folder"`. Version 1: adding an optional member later (e.g. NS-45's fingerprints) keeps version 1, since readers ignore unknown members; a breaking change bumps it, and any version but 1 reads as unreadable (tech-lead, 2026-10-18).
- **Two files, atomic writes:** under the history root (default `%LOCALAPPDATA%\NameShift\history`, created on the first write, never on a read), `last-batch.json` is the committed last batch and `pending.json` is a write-ahead marker. Each write goes to a `.tmp` file in the same folder, flushed to the disk, then `File.Replace` / `File.Move`, so a reader sees the old file or the new one, never a partial one. The store deletes only its own files; user items are never deleted (tech-lead, 2026-10-18).
- **Write-ahead:** `pending.json` is written before a batch or a revert moves anything, and removed after. If the journal can't be written before a batch, the batch is refused (not started), so nothing is ever renamed without a way to undo (tech-lead default, 2026-10-18; po may overrule).
- **Commit failure:** when `last-batch.json` can't be written after a successful batch or revert, the files stay as they are (correct), `pending.json` stays, the journal reads `Interrupted` and Revert is disabled; the result carries a warning (`JournalIssue`, `AfterFinish`) for NS-15 to show. A leftover `pending.json` with the id of `last-batch.json` is a harmless stale marker (tech-lead, 2026-10-18).
- **Reading:** any exception or invalid content makes the journal `Unreadable`; reading never writes, moves or deletes anything, so a corrupt file is kept untouched (tech-lead, 2026-10-18).
- **Partial revert:** a read-only plan first skips entries whose item is missing (`ItemMissing`), whose old name is taken by something else (`NameTaken`), or that wait for a skipped entry still holding their old name (`WaitsForSkippedItem`); linear in the batch size. The rest runs through NS-8's executor; when a run is refused or rolled back, the failed entry is skipped (`RenameFailed` for a failed rename call) and the rest runs again. The skipped entries stay in the journal, their new paths rebased onto reverted parent folders (one pass, the deepest folder), so Revert stays available for them (tech-lead, 2026-10-18).
- **After a full revert:** `last-batch.json` becomes `state reverted` with no paths (they are useless once reverted), and Revert is disabled until a new batch (tech-lead, 2026-10-18).
- **`NeedsAttention` keeps the journal:** when a revert step fails and its rollback fails too, `last-batch.json` is left unchanged; a later Revert skips the stranded items as missing and never overwrites (tech-lead, 2026-10-18).
- **A failed batch keeps an earlier interruption:** after a batch that does not succeed, `pending.json` is removed only when the disk is back to what `last-batch.json` describes: the batch was refused or rolled back, and the journal was not already `Interrupted` before it. After `NeedsAttention` (items stranded) or an earlier interruption it stays, so the journal reads `Interrupted` and Revert stays disabled; this amends the brief's step 5, which removed it after every failed batch and so could re-expose an older batch over a disk it no longer describes (tech-lead, 2026-10-18, from the NS-9 review).
- **A new operation overwrites a crashed operation's `pending.json`:** only its id is ever read back, so losing its human-readable list of what was in flight is accepted (tech-lead, 2026-10-18, from the NS-9 review).
- **Leftover `.tmp` files are not cleaned up:** a crash between the flush and the replace can leave a `<name>.<32 hex>.tmp` in the history folder; it is never read, harmless and rare, so no cleanup code is added (tech-lead, 2026-10-18, from the NS-9 review).
- **No cross-process lock:** `JournaledRenamer` handles one operation at a time and is not thread-safe; two windows (or NS-42's CLI undo) using the journal at once are out of scope here, to settle in NS-42 (tech-lead, 2026-10-18).

## Log
- 2026-10-18: created (po).
- 2026-10-18: set Ready, first round of the minimal usable slice (po).
- 2026-10-18: In Progress, branch feature/NS-9-journal (builder).
- 2026-10-18: In Review, PR #13 (builder).
- 2026-10-18: Done, PR #13 approved after review round 2 (tech-lead).
