# NS-30: Panels for Numbering, Extension, Name and Remove

Status: Draft
Depends on: NS-11, NS-17, NS-18, NS-19, NS-20, NS-21, NS-22, NS-23
Area: app

## Why
Use these rules from the window.

## Acceptance criteria
1. Each panel exposes every option of its rule and maps them to the rule options.
2. The Remove panel groups its parts (position, text, classes, tidy) in one panel with one enable checkbox.
3. Panels sit in pipeline order and each has enable and reset.
4. Invalid or empty fields are marked, never an exception.

## Out of scope
- Anything not listed above.

## Decisions
- One panel per rule story? **Decision:** panels come in groups so the engine and the UI stay separate, small PRs (po, 2026-10-18).

## Log
- 2026-10-18: created (po).
