# NS-55: Panels for Name and Extension

Status: Ready
Depends on: NS-11, NS-18, NS-19, NS-16
Area: app

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
8. Given the two new models, then `NamePanelModel.Stage` is `RuleStage.Name` and `ExtensionPanelModel.Stage` is
   `RuleStage.Extension`, and a `RulePanelHostModel` built from all six models (Replace, Case, Add, Numbering, Name,
   Extension, in any order given) lists them in pipeline order: Name (2) before Replace (3); Extension (11) after
   Numbering (9). The host sorts by stage itself; the test is in `NameShift.Ui.Tests`.
9. Given an enabled panel whose rule is null (Keep or Same), then nothing breaks and the preview is unchanged.
10. Mnemonics (rule: unique within a panel, panel titles distinct among themselves; test from NS-16 criterion 12):
    Name panel: title "Na&me" (M), mode label "Mod&e" (E), text label "&Text" (T). Extension panel: title
    "E&xtension" (X), mode label "&Mode" (M), text label "&Text" (T). The titles then are R, C, A, N, M, X, all
    distinct. The Extension note has no mnemonic. Mode texts live in small `*ModeText` helpers like `CaseModeText`, with a test that every mode has a
    non-empty text.
11. By hand (listed in the PR): both panels fit at 100 % and 200 % scale, tab order is mode list then text box, and
    a screen reader reads each panel's title and the disabled state.

## Out of scope
- Presets and command-line options: policy for every panel story: until NS-36 and NS-40 exist, the model keeps its
  settings as plain public properties and nothing more; once they exist, the story that adds a panel adds its fields
  (as NS-36 criterion 7 says).
- The Remove panel (NS-30).

## Decisions
- Why split from NS-30? **Decision:** NS-18 and NS-19 are ready now and a panel makes them usable; NS-30 keeps only
  the larger Remove panel (po, 2026-10-23).
- Text box disabled in modes that do not use it? **Decision:** yes, same pattern as the Case exceptions box (po,
  2026-10-23).
- Does the Extension panel validate the text? **Decision:** no; the rule cleans dots and spaces, and invalid
  characters show up as a conflict on the row (po, 2026-10-23).

- Alt-key letters for the new labels? **Decision:** unique within a panel only, repeats across panels allowed, titles
  distinct among themselves (David, 2026-10-23); letters in criterion 10. Depends on NS-16 for the changed test.

## Log
- 2026-10-23: created and set Ready (po).
- 2026-10-23: back to Draft: criterion 10 needs David's decision on Alt letters; criterion 8 made a model test; build-after line dropped (po, from the PR #24 review).
- 2026-10-23: David chose per-panel mnemonics; letters set, depends on NS-16 for the test; set Ready (po).
