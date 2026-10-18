# NS-31: Panels for RegEx, Move/Copy, Auto date and Append folder

Status: Draft
Depends on: NS-11, NS-24, NS-25, NS-26, NS-28
Area: app

## Why
Use the remaining rules from the window.

## Acceptance criteria
1. Each panel exposes every option of its rule.
2. The RegEx panel shows an error message under the pattern when it is invalid, live.
3. The Auto date panel offers the common formats in a list and a custom format box with an example of the result.
4. The pipeline order in the UI matches docs/PLAN.md (1 RegEx ... 11 Extension).
5. Every setting of these panels is saved in presets (NS-36) and has a command-line option (NS-40); where those stories are not built yet, this story adds the hook for them, and the reviewer checks it.

## Out of scope
- Anything not listed above.

## Decisions
- None needed.

## Log
- 2026-10-18: created (po).
