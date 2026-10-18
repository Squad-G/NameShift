# NS-12: Panels for Replace, Case and Add

Status: Ready
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

## Out of scope
- Panels for other rules (they come with their stories).

## Decisions
- Translation? **Decision:** English only at launch, strings kept in one resource file so a translation is possible later (po, 2026-10-18).

## Log
- 2026-10-18: created (po).
- 2026-10-18: set Ready, first round of the minimal usable slice (po).
