# NS-92: Move the settings-saving logic out of MainForm into NameShift.Ui

Status: In Review
Depends on: NS-37, NS-84
Area: app
Chore: no visible change; behaviour stays exactly as it is.

## Why
`MainForm.SaveSettings` holds real decisions (how the splitter is rounded, when a minimised window counts as maximised,
which folder is kept) that no test can reach. The rule of CLAUDE.md is: logic in `NameShift.Ui` with a test, thin controls.

## Acceptance criteria
A new static class in `NameShift.Ui.Settings` (indicative `SettingsCapture`) with tests on net10.0 and net48; `MainForm`
only reads its own facts and calls it. Its input is plain values (a small own enum for the window state: Normal, Maximized,
Minimized), never a WinForms type. The input also holds the collapsed-panel states (NS-84:
`IReadOnlyDictionary<RuleStage, bool>`, today `PanelCollapse.States`), which go into `AppSettings.CollapsedPanels` as they
are (a test: states in, same states out; none in, none out). `AppSettings` has no `Equals`, so "equal to" below means member
by member (window, split, last folder, collapsed panels), which a small test helper compares.
1. Given a Normal window with bounds 100, 50, 1200 x 800 at scale 1.5, then the saved window is
   `WindowPlacement.ToSaved(100, 50, 1200, 800, 1.5, maximized: false)`; a Maximized window uses its restore bounds and
   `maximized: true`; a Minimized window uses its restore bounds and is maximized exactly when it was maximised before it
   was minimised.
2. Given bounds with a width or height of 0 or less (a window that never showed), then the saved window is null.
3. Given the splitter distance 301 at scale 1.25, then the saved split is 241 (240.8 rounds up); 150 at scale 1.5 gives
   100; 101 at scale 2 gives 51 (the .5 case rounds away from zero, as `MidpointRounding.AwayFromZero`; banker's rounding
   would give 50); at scale 1 the distance is saved as it is. A negative result is saved as null.
4. Given a folder shown now, then it is the saved last folder; given none shown (a start that never showed a folder), then
   the folder read at start is kept instead of erasing it; given neither, null.
5. Given the result, then it is an `AppSettings` equal to what `MainForm.SaveSettings` builds today for the same facts
   (the tests pin the table of criteria 1 to 4 first, against the old code's results, written in the PR).
6. Given `MainForm.SaveSettings`, then it keeps its `try` and its silent `catch`, the 2 s limit and the rules saved with the
   settings; only the computation moved. The window and the saved files behave as before (by hand: move, resize, maximise,
   minimise from maximised, close and start again; written in the PR).

## Out of scope
- Any change to the settings file, what is saved, or when.

## Decisions
- Own enum for the window state? **Decision:** yes, so `NameShift.Ui` keeps no WinForms reference (CLAUDE.md) and the tests
  need no window (po, 2026-10-25).
- Why now? **Decision:** a rounding bug here only shows as a window that creeps by a pixel at every start on a high-DPI
  screen, which nobody would catch by hand (po, 2026-10-25).

## Log
- 2026-10-25: created from the NS-37 review follow-up; set Ready (po).
- 2026-10-25: In Progress (builder).
- 2026-10-25: In Review (builder): SettingsCapture in NameShift.Ui, MainForm calls it; Ui tests 2079 to 2106.
