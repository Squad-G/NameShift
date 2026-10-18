# NS-36: Save and load presets

Status: Draft
Depends on: NS-12
Area: app

## Why
Reuse a set of rules.

## Acceptance criteria
1. Given rules set, when I save a preset with a name, then a file is written in `%APPDATA%\NameShift\presets` as readable JSON.
2. Given I load a preset, then all panels get its values, enabled states included.
3. Given I delete a preset, then its file is removed after confirmation.
4. Given a preset with unknown or missing fields (older/newer version), then known fields load, others are ignored, no crash.
5. Given a preset name with invalid file characters, then it is stored under a safe file name but shown with the typed name.
6. Given a preset overwrites another, then the user is asked.

## Out of scope
- Sharing presets online.

## Decisions
- Format? **Decision:** JSON with a `version` field, because it is readable and needs no dependency (po, 2026-10-18).

## Log
- 2026-10-18: created (po).
