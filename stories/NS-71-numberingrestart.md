# NS-71: Numbering panel: restart in each folder

Status: Draft
Depends on: NS-60, NS-61
Area: app

## Why
With sub-folders on, number each folder from the start value again. (The panel part of the original NS-33:5.) Set Ready
when NS-60 (the engine option) is merged.

## Acceptance criteria
In `NumberingPanelModel` (on top of NS-61) and `NumberingPanel`:
1. Given a new panel, then `RestartPerFolder` is false and `IsDefault` is true; setting it true raises `Changed` once and
   makes `IsDefault` false; `Reset()` sets it false (a test for each).
2. Given the option is true, then the rule the model builds is `NumberingRule(..., breakPerFolder: true)` and false builds
   `breakPerFolder: false`; all other values as before. A test builds both and runs the pipeline on `C:\x\a.txt`,
   `C:\x\b.txt`, `C:\y\a.txt`, `C:\y\b.txt` with start 1 and suffix: `a_1, b_2, a_1, b_2` against `a_1, b_2, a_3, b_4`.
3. Given the window, then the Numbering panel has a check box `Restart in each folder`, unchecked, with its own Alt key (not
   used by another control, checked by the mnemonic test of NS-70 if it exists), after the number-system controls; it is
   enabled whenever the panel is. It has no effect when the list holds one folder (by hand: the names are the same).
4. Given Reset, then the check box clears and the preview updates once.
5. Given the text is in `Strings` and the accessible name is set. By hand at 150 % and 200 %: not clipped.

## Out of scope
- The sub-folders option itself (NS-33, NS-70), the engine (NS-60).

## Decisions
- Disabled unless sub-folders are on? **Decision:** no, it is always available and harmless: a disabled box needs an
  explanation and two panels would depend on each other (po, 2026-10-24).

## Log
- 2026-10-24: split out of NS-33 (po); Draft until NS-60 is merged.
