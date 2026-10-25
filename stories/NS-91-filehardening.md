# NS-91: Settings, presets and history: deep files, leftover temp files, no profile folder

Status: Ready
Depends on: NS-37, NS-36
Area: app
Chore: no visible change; three small hardening fixes from the NS-37 and NS-36 reviews, one concern: the files NameShift keeps.

## Why
A damaged or hostile settings file must not make the start slow, the files must not pile up leftovers, and a PC without a
profile folder must not break the start or write into the current directory.

## Acceptance criteria
Tests on net10.0 and net48. `AppSettingsJson` (settings.json), `PresetJson` (presets and last-rules.json), `DiskPresetFiles`
and the three default folders change; nothing else.
1. Given a file nested deeper than the real files ever are (for example 100,000 levels of `{"a":` inside the 1 MB limit),
   then `AppSettingsJson.Read` returns `AppSettings.Empty`, and `PresetJson.Read` gives its usual unreadable result, each in
   a test that fails when it does not finish in 5 seconds (a task with a timeout, not a hang). The reader quotas are
   `XmlDictionaryReaderQuotas.Max` copied, with `MaxDepth` set to 32 (copied, not new: the defaults would cap a long string at
   8,192 characters and silently drop a long preset).
2. Given the real files, then they still read back: settings.json with a window and a folder; a preset holding every panel
   with every setting at a non-default value and a 20,000-character text value (round trip through `Serialize` and `Read`).
   A valid file with an unknown member nested so that the whole document is exactly 32 levels deep reads, and one level more
   gives the unreadable result (the number is pinned by the test).
3. Given `DiskPresetFiles.WriteAtomic(path, ...)` (used by `PresetStore` and `AppSettingsStore`), then before it writes it
   deletes, in the same folder, the files named `<stem>.<32 hex digits>.tmp` (the exact name pattern of its own temporary
   files, same stem as the file being written, hex in lower case) whose last write time is over 1 hour old. It deletes nothing
   else: a test folder holds `settings.json`, `settings.txt`, `settings.abc.tmp`, `other.<32 hex>.tmp`, a matching fresh
   temp file (kept) and a matching old one (deleted, with `File.SetLastWriteTimeUtc`). The cleanup is best effort: a file that
   cannot be deleted (open, read-only) is skipped without an exception, and the write still happens. A folder that does not
   exist yet is not an error.
4. Given a cut-off save (the process ends after the temp file is flushed and before the replace), then the next save of the
   same file removes that leftover once it is over an hour old (the test creates it by hand).
5. Given the journal (`DiskJournalFiles`), then it is not changed: its leftover temp files stay a recorded decision of NS-9.
6. Given `Environment.GetFolderPath(LocalApplicationData)` or `ApplicationData` returns an empty or a relative text (a
   service account, a broken profile), then one shared helper in `NameShift.Disk` (indicative `AppDataFolders`, with a
   testable `Resolve(specialFolderPath, tempPath)`) gives `<temp>\NameShift` instead, and the three default folders use it:
   `JournaledRenamer.DefaultHistoryRoot` (`...\NameShift\history`), `AppSettingsStore.DefaultFolder` (`...\NameShift`) and
   `PresetStore.DefaultFolder` (`...\NameShift\presets`). When the special folder is normal, the three paths are exactly what
   they are today (a test pins them). Tests of `Resolve`: a normal path (with and without a trailing backslash), empty, white
   space, a relative text, and a drive root.
7. Given an empty profile folder, then `JournaledRenamer` no longer throws "must be a full path" with the default history
   root (a test builds one over `Resolve`'s fallback). No hand check is needed.

8. Given the journal reader (`JournalJson.Deserialize`, for `last-batch.json` and `pending.json`), then it gets the same
   quotas (copied from `Max`, `MaxDepth` 32) and the same two tests as criterion 1 and 2 (too deep gives `Unreadable`;
   a real journal still loads). A file of 100,000 levels may crash the process by stack depth instead of "not finishing", so
   the 32 versus 33 level rows are the real regression test; the deep file test runs in a child task with a timeout and a
   small stack budget in mind, and the PR says what it observed on both runtimes.

## Out of scope
- Cleaning the journal's temp files (still NS-9's decision); a message about the fallback folder; moving existing files from an old folder.

## Decisions
- Depth limit? **Decision:** 32: the real files are only a few levels deep, and 32 leaves room for a future field while a
  hostile file is refused at once (po, 2026-10-25).
- How old is a leftover? **Decision:** over 1 hour, so a save in progress in another window is never touched (it ends in
  milliseconds) and the rule needs no lock (po, 2026-10-25).
- No profile folder: stop or fall back? **Decision:** fall back to the temp folder: the window and the CLI keep working and
  Revert still has a journal; the history may be cleaned by Windows, which is acceptable for a PC in that state (po,
  2026-10-25).

- One PR or three? **Decision:** one: the three fixes are each a few lines in the same small set of files and tests, and
  splitting would triple the review round trips; the builder may split into commits (po, 2026-10-25, PR #90 review).

## Log
- 2026-10-25: PR #90 review: the journal reader's quota gap added (criterion 8) (po).
- 2026-10-25: created from the review follow-ups of NS-37 and NS-36; set Ready (po).
