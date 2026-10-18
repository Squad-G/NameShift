# NS-13: Live preview

Status: Done
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
- A new `PreviewModel` in NameShift.Ui owns the debounce and the off-thread run; it reads the host's `Rules` after the 150 ms delay and the file list's `Items`, cancels and discards stale runs by a version counter checked right after `await`, and keeps the old preview on screen while a new run is pending (tech-lead, 2026-10-18).
- A folder load runs the preview at once (no delay); a folder that is loading, failed or none clears the preview (tech-lead, 2026-10-18).
- A changed name is green **and bold** (bold is the non-colour cue), an unchanged name is grey, an error shows `Error: <message>` on one line in red; with a Windows contrast theme Changed and Error use the theme's text colour (tech-lead default, 2026-10-18; po may overrule).
- An unexpected failure of a run (not a rule error) clears the preview and shows the status bar message "Something went wrong. Try again." (tech-lead, 2026-10-18).

## Log
- 2026-10-18: created (po).
- 2026-10-18: set Ready, first round of the minimal usable slice (po).
- 2026-10-18: conflict display moved to NS-52 after review (po).
- 2026-10-18: In Progress (builder).
- 2026-10-18: In Review (builder): preview model in NameShift.Ui (150 ms debounce, off-thread pipeline, stale runs cancelled), New Name column green and bold / grey / Error text.

- 2026-10-18: Done (tech-lead): merged after VERDICT: APPROVE (round 2, after the review fixes); no nits left to fold. David's manual checks are pending, including criterion 4 (typing never lags with a 10 000-file folder).
