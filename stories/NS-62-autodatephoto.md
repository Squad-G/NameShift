# NS-62: Auto date: use the photo date

Status: Draft
Depends on: NS-26, NS-27, NS-59
Area: engine

## Why
Let the Auto date rule name a photo by the day it was taken (NS-27 reads the date).

## Acceptance criteria
Not final. Needs these design answers first (po, before Ready):
1. `RenameItem` gets an optional photo date, and `DateSource` a `Taken` value; `AutoDateRule` uses it.
2. When a file has no photo date: fall back to the modified date and tell the user in the preview (the leading option,
   because an error would block Rename for the whole batch, NS-15), which needs a note on the row (a way to show a non-error note is its own design: NS-46 only rewords the conflict texts).
3. Reading the files: only when the Auto date rule is enabled with `Taken`, off the UI thread, reading at most the
   first 256 KiB of each file, with the result kept until the file changes. A 10,000-file folder must not slow the
   listing (NS-58).

## Out of scope
- Anything else in NS-26 and NS-27.

## Decisions
- None yet.

## Log
- 2026-10-24: created when NS-27 was split: this is the rule and wiring part (po).
