# NS-12: Panels for Replace, Case and Add

Status: Done
Depends on: NS-11, NS-2, NS-3, NS-4
Area: app

## Why
Use the first three rules from the window.

## Acceptance criteria
1. Given the Replace panel with Find, With, Match case and Include extension, then values map to the Replace rule options.
2. Given the Case panel with a mode list (same, lower, upper, title, sentence) and an exceptions text box (comma separated), then they map to the Case rule.
3. Given the Add panel with prefix, suffix, insert text + position (from start or end) and the word-space checkbox, then they map to the Add rule.
4. Given all three panels enabled, then rules apply Replace, then Case, then Add.
5. Given an unset position (empty or not a number), then the field is marked and the Add insert part is ignored, never an exception.
6. Every visible string is a constant in one resource place, ready for translation.
7. Given the Add panel's insert counted from the end, then its position field is labelled so it is clear the number is how many characters stay after the inserted text (e.g. "characters after").
8. BuildRule never throws: invalid input (e.g. a non-numeric position) returns null, and a test proves it (tech-lead, from the NS-11 review).

## Out of scope
- Panels for other rules (they come with their stories).

## Decisions
- Translation? **Decision:** English only at launch, strings kept in one resource file so a translation is possible later (po, 2026-10-18).
- Case exceptions outside Title Case? **Decision:** the engine uses them only in Title mode, so the Exceptions box is greyed unless the mode is Title Case and keeps its text (tech-lead default, 2026-10-18; po may overrule).
- Models never throw: each setting's setter coerces null to an empty string, and BuildRule returns null when the rule would do nothing (tech-lead, 2026-10-18).
- Panels write into the model at once on every change and resync from the model on Changed; no timer here, NS-13 debounces (tech-lead, 2026-10-18).
- An unusable Add position is marked with an ErrorProvider icon and the box's accessible description, not colour alone; the label reads "Characters left after" when counting from the end, keeping the same mnemonic (tech-lead, 2026-10-18).
- Layout: one shared two-column table helper (RulePanelLayout), only system colours and fonts, the position box sized from its font (tech-lead, 2026-10-18).
- Mnemonics are unique across the window; the letters are fixed in the brief and checked by a test (tech-lead, 2026-10-18).

## Log
- 2026-10-18: created (po).
- 2026-10-18: set Ready, first round of the minimal usable slice (po).
- 2026-10-18: criterion 7 added, the from-the-end field's label (tech-lead, from the NS-4 review).
- 2026-10-18: criterion 8 added, BuildRule never throws (tech-lead, from the NS-11 review).
- 2026-10-18: In Progress (builder).
- 2026-10-18: In Review (builder): Replace, Case and Add panel models in NameShift.Ui, thin panels in the App registered in RulePanels.Create().
- 2026-10-18: Done (tech-lead): merged after VERDICT: APPROVE, review nits folded (the position box's accessible name follows its label, one shared AddLabeledRow helper, the Exceptions label greyed with its box, CasePanelModel.IndexOf, BOMs removed). David's manual checks M2–M10 are pending.
- 2026-10-18: Note for David's M7 check (150% / 200%): the position box width (font-measured) and its error-icon margin (LogicalToDeviceUnits) are already device pixels while MainForm scales with AutoScaleMode.Dpi, so they may be scaled twice; that would show as a wider box or margin, not clipping. FileList and RulePanelFrame use the same pattern: fix all three together if it shows (tech-lead, from the review).
