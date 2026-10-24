# NS-71: Numbering panel: restart in each folder

Status: In Review
Depends on: NS-60, NS-61
Area: app

## Why
With sub-folders on, number each folder from the start value again. (The panel part of the original NS-33:5.) NS-60
(`NumberingRule.BreakPerFolder`) and NS-61 are merged, so the check box and its model land now; it only changes names
once sub-folders can be listed (NS-74), until then it is harmless.

## Acceptance criteria
In `NumberingPanelModel` (on top of NS-61) and `NumberingPanel`:
1. Given a new panel, then `RestartPerFolder` is false and `IsDefault` is true; setting it true raises `Changed` once and
   makes `IsDefault` false; `Reset()` sets it false (a test for each).
2. Given the option is true, then the rule the model builds is `NumberingRule(..., breakPerFolder: true)` and false builds
   `breakPerFolder: false`; all other values as before. A test builds both and runs the pipeline on four items
   `new RenameItem("a.txt", @"C:\x")`, `("b.txt", @"C:\x")`, `("a.txt", @"C:\y")`, `("b.txt", @"C:\y")` with start 1 and suffix: `a_1, b_2, a_1, b_2` against `a_1, b_2, a_3, b_4`.
3. Given the window, then the Numbering panel has a check box `Restart in each folder`, unchecked, in the
   panel's last row, after the `At character` row (the rows today: Place, Number system, Letter case, Start, Count by,
   Digits, Text between, At character), enabled whenever the panel is. Its text is `Strings.NumberingRestartPerFolder` with its own Alt
   key: the new string is added to the Numbering panel's label list in the mnemonic test of
   `FirstPanelsTests` (`MnemonicCheck.Problems` stays empty: no letter shared with the panel's title or its other labels).
   It has no effect when the list holds one folder (a test: one folder, option on or off, the same names).
4. Given Reset, then the check box clears and the preview updates once (the panel's `Reset()` and the host's Reset
   button, by hand for the button).
5. Given the window, then the check box's accessible name is `RulePanelText.PlainTitle(Strings.NumberingRestartPerFolder)`
   (the label without `&`, as `RulePanelLayout` does for the other controls). Alt letters already taken in the panel:
   N, L, V, Y, G, B, H, S, C (the new one is none of them). By hand at 150 % and 200 %: not clipped, and the panel's other
   controls keep their places.
6. Given the presets of NS-36 (panel `SaveSettings`/`LoadSettings`), then `RestartPerFolder` is part of the Numbering
   panel's saved settings: whichever of NS-36 and NS-71 merges second adds it, with a round-trip test (set true, save,
   reset, load, true again), and an old preset without the value loads as false.

## Out of scope
- The sub-folders option itself (listing NS-74, box NS-78), the engine (NS-60).

## Decisions
- Disabled unless sub-folders are on? **Decision:** no, it is always available and harmless: a disabled box needs an
  explanation and two panels would depend on each other (po, 2026-10-24).

## Log
- 2026-10-24: split out of NS-33 (po); Draft until NS-60 is merged.
- 2026-10-24: NS-60 and NS-61 merged; mnemonic test pointed at `FirstPanelsTests`; set Ready (po).
- 2026-10-24: In Progress (builder).
- 2026-10-24: In Review (builder), PR #68.
