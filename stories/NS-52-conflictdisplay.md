# NS-52: Conflict display

Status: Done
Depends on: NS-13, NS-5, NS-6, NS-7
Area: app

## Why
Know which names cannot be used, and why, before pressing Rename.

## Acceptance criteria
1. Given any conflict flag from NS-5, NS-6 or NS-7 (InvalidCharacter, ReservedName, TrailingDotOrSpace, EmptyName, NameTooLong, DuplicateTarget, TargetExists, PathTooLong), then the row is marked red and a Status column states the reason in plain words.
2. Given conflicts, then a summary under the list says `N conflicts` (and nothing when there are none).
3. Given a conflict, then it is also shown by an icon or text, never by colour alone (accessibility).
4. Given a `TargetExists` flag, then it is shown as NS-6 computes it (where the existing names come from is defined once, in NS-6:3).
5. Given a file that is not selected (NS-14), then its Status is empty; it can still cause `TargetExists` on a selected file.
6. Given a conflict whose detail contains a control character (NS-5 returns it raw, e.g. a tab), then it is shown escaped (e.g. `U+0001`), never raw.

## Out of scope
- Disabling the Rename button (NS-15 owns that).
- Plain-sentence refinements of the messages (NS-46).

## Decisions
- Where does "Rename disabled on conflicts" live? **Decision:** only in NS-15, so it is written once (po, 2026-10-18).
- The summary counts rows with at least one conflict (a row with two codes counts once); rule errors (Error results) are excluded, they have their own text in New Name (tech-lead default, 2026-10-18; po may overrule).
- Message wording: `Not allowed in a name: {0}`, `Reserved by Windows: {0}`, `Ends with a dot or a space`, `Name is empty`, `Name is too long`, `Same new name as another file`, `A file with this name already exists`, `Full path is too long`, joined by `; `; summary `1 conflict` / `N conflicts` (tech-lead default, 2026-10-18; po may overrule). NS-46 rewrites them in `ConflictText` only.
- The existing names for NS-6:3 come from the same disk pass as the file list (`DiskFileListSource` collects every entry name, hidden and system included, into `FileListing.ExistingNames`); no second listing and no extra disk call (tech-lead, 2026-10-18).
- The three checks run inside the preview's one `Task.Run`, behind its cancellation token and version check; the snapshot carries the `ConflictReport` (tech-lead, 2026-10-18).
- All conflict text lives in `ConflictText` (one place for NS-46); control characters in a detail are escaped as `U+XXXX` (tech-lead, 2026-10-18).
- The summary is a label under the list inside the file list control, not the status bar, which routine messages overwrite (tech-lead, 2026-10-18).

## Log
- 2026-10-18: created, split from NS-13 (po).
- 2026-10-18: set Ready, first round of the minimal usable slice (po).
- 2026-10-18: criterion 6 added, control characters in a conflict's detail are shown escaped (tech-lead, from the NS-5 review).
- 2026-10-18: In Progress, branch feature/NS-52-conflicts (builder).
- 2026-10-18: In Review. Checks run in the preview's run (`PreviewModel.Compute`), the snapshot carries the `ConflictReport` (`ConflictCount`, `ErrorCount`); the full listing comes from the same disk pass as the list (`FileListing.ExistingNames`); Status column plus a `N conflicts` summary under the list. Criterion 5's "Status empty for unselected files" half arrives with NS-14 through `PreviewModel.TryGetRow`; the "still causes TargetExists" half is covered now (`NameOutsideTheBatch_IsTargetExists`). Builder.
- 2026-10-18: Done (tech-lead): merged after VERDICT: APPROVE, with the three review nits folded in (a conflict check that throws ends as `HasFailed`; the 10 000-item test asserts 5 000 conflict rows; `StaleRun_ConflictsAreDiscarded` now waits until the old run is blocked on its last item, so it pins the run's cancellation and version checks). David's manual checks are pending; criterion 5's "Status empty when unselected" half comes with NS-14.
- 2026-10-18: criterion 5's second half (an unselected file has an empty Status and can still cause `TargetExists`) is now delivered by NS-14 (builder).
