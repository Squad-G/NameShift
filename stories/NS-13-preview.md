# NS-13: Live preview

Status: In Progress
Depends on: NS-51, NS-11, NS-1
Area: app

## Why
See every new name before anything is touched. Conflict marking is NS-52.

## Acceptance criteria
1. Given any rule setting changes, then the New Name column updates for all files in the folder without clicking anything.
2. Given I type in any rule field, then the preview updates as I type; there is no Apply or Preview button.
3. Given a file whose name changes, then its New Name cell is shown in a distinct colour (green family, plus a bold or marker so colour is not the only cue); unchanged names are shown in a muted colour.
4. Given 10 000 files and a typing user, then computing is done off the UI thread, stale results are discarded, and typing never lags (checked by hand).
5. Given the folder changes, then the preview is recomputed for the new list.
6. Given a rule produced an error for an item (NS-1), then the row shows the error text in the New Name cell instead of a name.

## Out of scope
- Conflict flags, status column and summary (NS-52).

## Decisions
- Recompute on every keystroke? **Decision:** the preview debounces 150 ms and is the only place that debounces (the panels notify at once, NS-11), because it feels live and avoids wasted work (po, 2026-10-18).
- Split from conflict display because computing off-thread with stale-result handling is a full PR (po, 2026-10-18).

## Log
- 2026-10-18: created (po).
- 2026-10-18: set Ready, first round of the minimal usable slice (po).
- 2026-10-18: conflict display moved to NS-52 after review (po).
- 2026-10-18: In Progress (builder).
