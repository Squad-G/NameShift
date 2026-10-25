# NS-95: Cold start: the first window gathers its own paths with the later ones

Status: In Review
Depends on: NS-88, NS-89
Area: app

## Why
Explorer can start one `NameShift.exe` per selected file. With no window running, the first process becomes the window and
opens its own path at once; the others arrive by the pipe and replace the selection (NS-88 criterion 5). The first file
ends up deselected, so Rename would touch fewer files than the user picked, or a different set. The owner window must feed
its own start arguments into the same `RequestGatherer` as the piped requests, so the end state is the same whether a window
was already running or not. Found in the review of NS-49 (PR #107).

## Acceptance criteria
Logic in `NameShift.Ui` with tests on net10.0 and net48 (the fake clock of NS-89's `RequestGatherer` tests); `MainForm` and
`Program` only wire it. Names are indicative.
1. Given the owner window starts with path arguments, then those arguments enter the same `RequestGatherer` as a request
   (with the process's own current directory), as the first request, and are applied together with the requests that
   arrive within the gathering time (300 ms of quiet, 3 s at most, NS-89 criterion 5). They are no longer opened
   directly. The decision is made in `NameShift.Ui`: `StartFolderChoice.Pick` returns a new value (indicative `Gather`)
   instead of `Argument` when the start has a path argument, and a Ui helper builds the owner's `InstanceRequest` from
   the start arguments and the process's own current directory; `MainForm.OnShown` only calls `Add` and
   `ScheduleGatherCheck` for it. Tests: `Pick` with an argument gives `Gather` (the old `Argument` value is gone, so the
   test fails without the change), and the helper's request carries every argument and the current directory. The
   gatherer's cap of 2,000 paths (NS-89) now applies to the owner's own arguments too: new behaviour, stated here
   (before, NS-88 criterion 8 used every argument; a command line holds far fewer in practice).
2. Given a cold start of 15 processes, each with one file of one folder (a test: the owner's start request followed by 14
   piped requests inside the quiet time), then the gathered result is one folder with all 15 files selected, the owner's file
   included. With a slow trickle cut at 3 s, the first batch contains the owner's request.
3. Given the owner starts with one path and nothing else arrives, then the result is what NS-47 gives today (a folder
   opens, a file opens its folder and selects it, messages as before); the only difference is the gathering delay of at
   most 300 ms for a start with a path. While the gathering runs (up to 300 ms), the window neither calls
   `_navigator.Start()` (Documents never flashes first) nor opens the remembered folder. A gathered batch that resolves
   to nothing (path not found) ends as today: the message, then the start folder through `OpenAction.OpenStartFolder`.
4. Given the owner starts with no path argument, then there is no gathering delay (NS-89 criterion 5, last sentence) and the
   window opens as usual (settings, last folder).
5. Given the gathered requests of several folders, then the folder of the first request wins (the owner's), as NS-89 says.
6. Given the window is locked (a Rename or Revert is running) when the gathered batch is due, then NS-89 criterion 4 applies
   as before (cannot happen at the first start; the test still covers the rule).
7. By hand, written in the PR: with no window running, 15 starts in a command prompt loop (`for %f in (D:\Pics\*.jpg) do start
   "" NameShift.exe "%f"`) give one window with all 15 rows selected, the first file included; the same with 2 files; a
   single start opens as before; closing the window and repeating works.

## Out of scope
- The Explorer entry and the installer (NS-44, NS-49).
- Changing the gathering times, the pipe or the request format.

## Decisions
- Where is the fix? **Decision (lead, 2026-10-25):** a small story in the app and the Ui logic, not inside the installer
  story: it is app code with its own tests, and NS-49 depends on it (po, 2026-10-25).
- Delay for one path? **Decision:** accepted: at most 300 ms before a single folder or file opens at the first start, because
  the owner cannot know whether more paths are coming (po, 2026-10-25).

## Log
- 2026-10-25: created from the review of PR #107 (blocker 2: NS-49 criterion 4 was not met by the merged NS-89 on a cold
  start) (po). Ready.
- 2026-10-25: PR #107 re-review nits folded in: the gather decision in `NameShift.Ui` (`Pick` gives `Gather`, a helper
  builds the owner's request, tests that fail without the change), nothing shown during the gathering and the not-found
  end as today, the 2,000-path cap stated for the owner (tech-lead).
- 2026-10-25: built (builder): StartFolderSource.Argument became Gather, StartGathering helper, OnShown gathers; PR open, In Review.
