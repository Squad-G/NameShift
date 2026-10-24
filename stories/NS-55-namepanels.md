# NS-55: Panels for Name and Extension

Status: Ready
Depends on: NS-11, NS-18, NS-19
Area: app
Build after: NS-18 and NS-19 are merged (it uses both rule classes).

## Why
Use the Name and Extension rules from the window as soon as they exist, instead of waiting for every Remove rule.

## Acceptance criteria
Same pattern as NS-12: a model in `NameShift.Ui.Rules` (extends `RulePanelModel`), a thin `UserControl` in
`src/NameShift.App/Controls/`, one line each in `RulePanels.Create()`, every string in `NameShift.Ui.Strings`.
1. Given the Name panel with a mode list (Keep, Fixed text, Remove name, Reverse) and a text box, then `BuildRule`
   returns a `NameRule` with the chosen mode and text; in Keep mode it returns null; `IsDefault` is true only for
   Keep with an empty text. The default mode is Keep.
2. Given the Name panel in a mode other than Fixed, then the text box is disabled and keeps its text (as the Case
   exceptions box); model exposes `TextApplies` and a test checks it.
3. Given the Extension panel with a mode list (Same, Lower case, Upper case, Fixed, Extra, Remove) and a text box,
   then `BuildRule` returns an `ExtensionRule` with the chosen mode and text; in Same mode it returns null;
   `IsDefault` is true only for Same with an empty text. The default is Same.
4. Given the Extension panel in a mode other than Fixed or Extra, then the text box is disabled and keeps its text.
5. Given the Extension panel, then a visible note reads that only the name changes and the file content is not
   converted (string from `Strings`, wording: "Only renames the file. It does not convert its content.").
6. Given each panel's reset, then the mode and text return to the defaults and the panel is unchecked (the NS-11
   behaviour), and `Changed` fires once.
7. Given both panels enabled with Name = Fixed `trip` and Extension = Lower on `IMG_9.JPG`, then the preview shows
   `trip.jpg` (a model-level pipeline test builds both rules and runs them).
8. Given the panel stack, then the panels sit in pipeline order: Name (2) before Replace (3); Extension (11) after
   Numbering (9). A test on `RulePanels.Create()` checks the stages are ascending.
9. Given an enabled panel whose rule is null (Keep or Same), then nothing breaks and the preview is unchanged.
10. Mnemonics stay unique across the window (the existing test is extended to the new labels); mode texts live in
    small `*ModeText` helpers like `CaseModeText`, with a test that every mode has a non-empty text.
11. By hand (listed in the PR): both panels fit at 100 % and 200 % scale, tab order is mode list then text box, and
    a screen reader reads each panel's title and the disabled state.

## Out of scope
- Presets and command-line options for these settings (NS-36, NS-40 add them).
- The Remove panel (NS-30).

## Decisions
- Why split from NS-30? **Decision:** NS-18 and NS-19 are ready now and a panel makes them usable; NS-30 keeps only
  the larger Remove panel (po, 2026-10-23).
- Text box disabled in modes that do not use it? **Decision:** yes, same pattern as the Case exceptions box (po,
  2026-10-23).
- Does the Extension panel validate the text? **Decision:** no; the rule cleans dots and spaces, and invalid
  characters show up as a conflict on the row (po, 2026-10-23).

## Log
- 2026-10-23: created and set Ready (po).
