# NS-8: Safe rename executor

Status: Ready
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

## Log
- 2026-10-18: created (po).
- 2026-10-18: set Ready, first round of the minimal usable slice (po).
