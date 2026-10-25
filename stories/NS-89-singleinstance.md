# NS-89: One window at all times

Status: Ready
Depends on: NS-88
Area: app

## Why
Starting NameShift again (a shortcut, the command line, Explorer) brings the running window to the front on the new folder
and selection instead of opening a second window. Explorer can start one process per selected file; the user must still
see one window with all of them selected. This is the second piece of the Explorer entry (NS-49).

## Acceptance criteria
Logic in `NameShift.Ui` (the request, its format, its limits, the gathering) with tests on net10.0 and net48; the mutex, the
pipe and the foreground code are thin, in `NameShift.App`. The mechanism is the tech lead's (the default: a named mutex and
a named pipe per user session). Names are indicative.
1. Given a window is running, then a second `NameShift.exe` start (same Windows user session) hands its request to it and
   exits with code 0 within about a second, showing no window. The running window is restored if minimised and brought to
   the front (the second process, which has the user's input, lets the window take the foreground).
2. Given the request, then it carries the arguments as given and the second process's current directory, so a relative
   path means what it meant where the user typed it. The window handles it like a start with those arguments (NS-47 and
   NS-88: folder, selection, status bar message).
3. Given no path argument, then the window only comes to the front; nothing else changes.
4. Given a Rename or Revert is running (the window is locked), then the window comes to the front, the request is ignored
   and the status bar says `NameShift is busy. Open the folder again when it is done.` Nothing is queued.
5. Given a burst of starts (what Explorer can do for a selection with one process per item: for example 15 processes
   within a second, each with one path), then the end state is one window with all those paths selected. The window gathers: it
   applies the requests together once none arrived for 300 ms, at most 3 s after the first (a `RequestGatherer` with a fake
   clock tests: one request; a burst of 15 inside the quiet time; a slow trickle cut at 3 s; requests of different folders
   gather into the folder of the first). No gathering delay applies to a start without a path argument.
6. Given no window is running and several processes start at once, then exactly one becomes the window (the mutex decides),
   the others hand over their request as in criterion 1 after the pipe is ready (they retry for up to 5 s).
7. Given a request from the pipe, then it is untrusted: the pipe is open to the current user only; a message over 4 MiB or
   with more than 100,000 paths, or one that cannot be read, is ignored without a crash; every path goes through
   `OpenTarget` (its checks and its messages). A request never renames anything.
8. Given the running window does not answer within 3 s (hung, or a crashed owner left the mutex), then the new process
   starts its own window rather than doing nothing. An abandoned mutex is taken over without an exception.
9. Given two Windows users on one PC, then each has their own window (the mutex and the pipe are per session and per user).
10. Given `NameShiftCmd.exe`, then it is not affected and can run beside the window.
11. Given the settings (NS-37), then they are saved by the one window at its close, never by two.
12. By hand, written in the PR: start twice; start with a folder while the window is minimised; start while a Rename runs;
    15 starts in a command prompt loop with 15 files of one folder: one window, 15 rows selected; closing the window and
    starting again opens normally.

## Out of scope
- The registry entry and how Explorer calls the program (NS-49); more than one level of requests; reusing the window for
  `NameShiftCmd`.

## Decisions
- Always one window? **Decision:** yes (earlier decision of NS-49, kept): nothing is lost by switching the folder, because
  nothing changes until Rename is pressed (po, 2026-10-18; 2026-10-25).
- A request while busy? **Decision:** ignored with a message: simpler and safer than queueing a folder change behind a
  Rename (po, 2026-10-25).
- A window that does not answer? **Decision:** open a second window: a user who clicked must get a window (po, 2026-10-25).
- Gathering times? **Decision:** 300 ms of quiet and 3 s at most: long enough for Explorer's burst, short enough to feel
  instant for one path; tunable by the tech lead if a measurement says otherwise (po, 2026-10-25).

## Log
- 2026-10-25: created from NS-49's split (po).
