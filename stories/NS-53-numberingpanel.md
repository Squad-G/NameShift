# NS-53: Numbering panel

Status: Ready
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

## Out of scope
- Hex, letters, roman numerals and break per folder (NS-29).

## Decisions
- Default separator? **Decision:** `_`, the most common choice in file names (po, 2026-10-18).

## Log
- 2026-10-18: created after review so numbering is in the first round (po).
- 2026-10-18: set Ready (po).
