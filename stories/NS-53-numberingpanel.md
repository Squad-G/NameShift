# NS-53: Numbering panel

Status: In Progress
Depends on: NS-11, NS-17
Area: app

## Why
Use the numbering rule (NS-17) from the window, so the first usable version can number files.

## Acceptance criteria
1. Given the Numbering panel with mode (prefix, suffix, insert at position), start, step, pad (digits), separator and, for insert, a position, then the values map to the Numbering rule options and the panel sits at stage 9 in the pipeline order.
2. Given an empty or non-numeric start, step or pad, or an insert position below 1, then the field is marked and the rule is ignored for that item, never an exception.
3. Given the panel's reset, then it returns to defaults (suffix, start 1, step 1, pad 0, separator `_`) and is unchecked.
4. Given I select only some files (NS-14), then numbering follows the selected files in list order.
5. Given the settings, then they are saved in presets (NS-36) and have a command-line option (NS-40) as soon as those stories exist.
6. BuildRule never throws: invalid input (e.g. a non-numeric position) returns null, and a test proves it (tech-lead, from the NS-11 review).

## Out of scope
- Hex, letters, roman numerals and break per folder (NS-29).

## Decisions
- Default separator? **Decision:** `_`, the most common choice in file names (po, 2026-10-18).
- Insert counted from the end (as in Add)? **Decision:** it isn't offered for numbering; a later story if asked (tech-lead, from the NS-17 review).
- Numeric fields: TextBox or NumericUpDown? **Decision:** TextBoxes. Criterion 2 needs empty or non-numeric input to stay visible and be marked; NumericUpDown silently reverts bad input, commits only on focus loss or Enter (so the model would not update at once, as NS-12 decided) and its spin buttons scale poorly in .NET Framework at 150-200%. The model holds the text as typed (tech-lead, 2026-10-18).
- Valid values? **Decision:** start = whole number from 0 (a negative start makes every item an engine error, so the panel marks it); step = any whole number (0 and negative allowed, as in the engine); pad = whole number 0 to 255 (outside is marked and never passed to the engine); position (Insert only) = `TextPosition.Parse`. Start, step and pad are read with `int.TryParse` (invariant culture, ASCII digits, optional sign; overflow is invalid). Any invalid field that applies makes `BuildRule()` return null and marks the field. A negative step that drives later numbers below 0 is not marked: the engine reports those items as errors in the preview (NS-13) (tech-lead, 2026-10-18).
- Defaults? **Decision:** mode Suffix, start 1, step 1, pad 0, separator `_` (the engine's own is empty), position empty and marked only in Insert mode (tech-lead, 2026-10-18).
- The position field is greyed outside Insert mode? **Decision:** yes, with its label; it keeps its text, and outside Insert the rule gets no position (tech-lead, 2026-10-18).
- Mode control? **Decision:** a drop-down list, like the Case panel (tech-lead, 2026-10-18).
- Labels and mnemonics? **Decision:** NS-12 uses R F W M X C O T A P I S D U E, which takes every letter of "Start", "Step", "Separator", "Position" and "Mode", so the labels are worded around free letters: N (Numbering), L (Place), V (Start value), Y (Count by), G (Digits), B (Text between), H (At character). J K Q Z stay free (tech-lead default, 2026-10-18; po may overrule; question logged for David).
- Separator trimmed? **Decision:** no, a space is a valid separator; characters invalid in file names are left to the conflict checker (tech-lead, 2026-10-18).

## Log
- 2026-10-18: created after review so numbering is in the first round (po).
- 2026-10-18: set Ready (po).
- 2026-10-18: criterion 6 added, BuildRule never throws (tech-lead, from the NS-11 review).
- 2026-10-18: In Progress (builder).
- 2026-10-18: criterion 5: NS-36 and NS-40 are Draft: the model's settings are plain properties ready for presets and the command line; nothing to wire yet (builder).
