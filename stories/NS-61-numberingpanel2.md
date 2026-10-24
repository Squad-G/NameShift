# NS-61: Numbering panel: number system and restart per folder

Status: Draft
Depends on: NS-53, NS-29, NS-60
Area: app

## Why
Use the number systems of NS-29 and the folder restart of NS-60 from the Numbering panel.

## Acceptance criteria
Not final; to be written when NS-29 is built. It will cover: a drop-down for the system (decimal, hexadecimal, letters,
roman) and a case choice; the start value typed in that system and read with `NumberText.TryParse` (marked when it does
not parse); the digits field disabled for letters and roman; a "restart in each folder" checkbox; new Alt letters free
in the Numbering panel (taken: N L V Y G B H); reset, `IsDefault` and `BuildRule` updated, with one test per setting.

## Out of scope
- Saving in presets and command-line options (NS-36, NS-40).

## Decisions
- None yet.

## Log
- 2026-10-24: created when NS-29 was split (po).
