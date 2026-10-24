# NS-30: Panel for Remove

Status: Draft
Depends on: NS-11, NS-20, NS-21, NS-22, NS-23
Area: app

## Why
Use the Remove rules from the window. (The Name and Extension panels moved to NS-55, 2026-10-23.)

## Acceptance criteria
1. The panel exposes every option of its rules and maps them to the rule options.
2. The Remove panel groups its parts (position, text, classes, tidy) in one panel with one enable checkbox.
3. The panel sits in pipeline order and has enable and reset.
4. Invalid or empty fields are marked, never an exception.
5. Every setting of this panel is saved in presets (NS-36) and has a command-line option (NS-40); where those stories are not built yet, the panel model keeps its settings as plain public properties and nothing more (the same policy as NS-16 and NS-55); the story that builds presets or the command line adds the fields.

## Out of scope
- Anything not listed above.

## Decisions
- One panel per rule story? **Decision:** panels come in groups so the engine and the UI stay separate, small PRs (po, 2026-10-18).

## Log
- 2026-10-18: created (po).
- 2026-10-23: Name and Extension panels split out to NS-55; this story is now the Remove panel only (po).
