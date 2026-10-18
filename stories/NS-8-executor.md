# NS-8: Safe rename executor

Status: In Progress
Depends on: NS-1, NS-5, NS-6, NS-7
Area: app

## Why
Turn a checked plan into real renames without losing a file, even for swaps and chains.

## Acceptance criteria
1. Given a plan with any conflict flag, then the executor refuses to start and changes nothing.
2. Given `a`->`b` and `b`->`c` in one folder, then both renames succeed in an order that never overwrites (the file whose target is free goes first: `b`->`c`, then `a`->`b`), verified in a temp folder.
3. Given a swap `a`<->`b`, then it succeeds through a temporary name and both files keep their own content.
4. Given a case-only change `photo.jpg` -> `Photo.jpg`, then the file name changes on disk (via a temporary name).
5. Given the 3rd of 5 renames fails (file locked or removed), then the already-done renames are reverted, the 5 files are back to their old names, and the result reports which file failed and why.
6. Given the rollback itself fails for a file, then the result lists it as `NeedsAttention` with both names; nothing is deleted.
7. Given a source file changed between the preview and the click (it no longer exists), then the executor refuses that batch before renaming anything.
8. Given a target name that is not part of the batch already exists on disk when the rename runs (a file created after the preview, or a hidden or system file the list never showed), then that rename is never performed: nothing is overwritten, the batch stops, everything already renamed is rolled back and the result names the file in the way. The executor checks the target on disk right before each rename (a check at the preview is not enough) and uses a rename call that fails rather than replaces.
9. Given folders in the plan, then they are renamed like files, children are never needed because paths are rebuilt after parents.

## Out of scope
- Writing the undo journal (NS-9), copying or moving files to another folder (dropped: NameShift only renames, NS-34).

## Decisions
- Never overwrite, whatever the source of a collision. **Decision:** this is the product's core safety rule, enforced here at run time and not only by the preview checks (po, 2026-10-18).
- On failure: stop or continue? **Decision:** stop and roll back the whole batch, because a half-renamed folder is the worst outcome for numbered sets (po, 2026-10-18).
- Where does this code live? Technical choice left to the tech lead; it must be testable against a temp folder (po, 2026-10-18).
- **Where the code lives:** a new library `src/NameShift.Disk` (netstandard2.0, references `NameShift.Core`), referenced by `NameShift.App`. It is the app's disk layer: the engine stays pure, and "only the app touches the disk" holds through this library; a library rather than code in the WinExe so it can be tested (tech-lead, 2026-10-18).
- **Tests:** a new project `tests/NameShift.Disk.Tests` targeting `net10.0;net48`, because .NET Framework 4.8 (the runtime the app ships on) refuses a case-only `Directory.Move` that .NET 10 accepts; the Core tests stay pure and untouched (tech-lead, 2026-10-18).
- **Ordering:** per folder, a rename whose target is free goes first; a cycle (swap, rotation, and a case-only change, a cycle of one) is broken by moving one member to a temporary name in the same folder. Deterministic: ties go in plan order (tech-lead, 2026-10-18).
- **Temporary names:** `~ns-<token>-<n>` (8 lowercase hex chars new for each Execute call; n = 1, 2, … for the whole call), skipping a name that exists on disk or is an old or new name of the batch in that folder (tech-lead, 2026-10-18).
- **A folder and items inside it in the same batch: allowed.** Deepest folders first, so every rename happens while its folder still has its old name; the output's new paths are rebuilt after parents (tech-lead, 2026-10-18).
- **Nothing is ever deleted:** the file-system seam has no delete method; the executor never calls `File.Delete` or `Directory.Delete` (tech-lead, 2026-10-18).
- **Rule errors in the plan:** a plan where any result has `Status == Error` is refused like a conflict (`HasRuleErrors`), because a partial batch breaks sets (numbering gaps) and the user sees the error in the preview first; NS-15 should disable Rename on rule errors too (tech-lead default, 2026-10-18; po may overrule).

## Log
- 2026-10-18: created (po).
- 2026-10-18: set Ready, first round of the minimal usable slice (po).
- 2026-10-18: In Progress (builder).
