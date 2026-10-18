# NS-13: Live preview and conflict display

Status: Ready
Depends on: NS-10, NS-11, NS-1, NS-5, NS-6, NS-7
Area: app

## Why
See every new name before anything is touched.

## Acceptance criteria
1. Given any rule setting changes, then the New Name column updates for all files in the folder without clicking anything.
2. Given a file whose name changes, then its New Name cell is shown in a distinct colour (green family, plus a bold or marker so colour is not the only cue); unchanged names are shown in a muted colour.
3. Given a conflict (invalid character, reserved name, duplicate, exists, too long), then the row is marked red and a Status column states the reason in plain words.
4. Given at least one conflict, then a summary under the list says `N conflicts`, and the Rename button (NS-15) is disabled.
5. Given 10 000 files and a typing user, then computing is done off the UI thread, stale results are discarded, and typing never lags (checked by hand).
6. Given the folder changes, then the preview is recomputed for the new list.
7. Given I type in any rule field, then the preview updates as I type; there is no Apply or Preview button.
8. Given colours, then conflicts are also shown by an icon or text, never by colour alone (accessibility).

## Out of scope
- Anything not listed above.

## Decisions
- Recompute on every keystroke? **Decision:** debounce 150 ms, because it feels live and avoids wasted work (po, 2026-10-18).

## Log
- 2026-10-18: created (po).
- 2026-10-18: set Ready, first round of the minimal usable slice (po).
