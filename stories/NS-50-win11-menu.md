# NS-50: "Bulk Rename Here" in the Windows 11 top-level menu

Status: Draft
Depends on: NS-49
Area: installer

## Why
On Windows 11 the classic entry hides under "Show more options"; users would find it faster in the compact menu.

## Acceptance criteria
1. Given Windows 11, then "Bulk Rename Here" shows in the compact right-click menu for folders, the folder background and file selections, with the same behaviour as NS-49.
2. Given Windows 10, then nothing changes: the classic entry from NS-49 is used.
3. Given the installer is built with this feature, then it is still at most 3 MB, or this story is not done (the size is measured and written in the log).
4. Given uninstall, then the extension and its registration are removed and Explorer needs no restart to show the change after sign-out at most.
5. Given the compact entry is unavailable for any reason, then the classic entry still works.

## Out of scope
- Any other shell integration.

## Decisions
- Do it at all? **Decision:** only if it keeps the installer small and needs no admin rights, because the compact menu needs a packaged shell extension, which is heavier and may need signing; if it fails either test, this story is cancelled and the classic entry stays (po, 2026-10-18).
- Order? **Decision:** last in the backlog, after the classic entry has been used for a while (po, 2026-10-18).

## Log
- 2026-10-18: created (po).
