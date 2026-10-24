# NS-75: Command line: Name, Extension and Remove options

Status: Draft
Depends on: NS-40, NS-55, NS-30
Area: app

## Why
Second batch of rule options for the command line: the Name and Extension rules and the Remove rule. Split from the
original NS-40.

## Acceptance criteria
To write when NS-40 is merged (po), following its pattern: one flag per setting of `NamePanelModel`,
`ExtensionPanelModel` and `RemovePanelModel`, values refused with the model's own error, a parity test through
`PreviewModel` for every option, every option in the help (NS-43's test), any order, no repeats.

## Out of scope
- RegEx, Move/Copy, Auto date, Folder name (NS-76).

## Decisions
- None yet.

## Log
- 2026-10-24: created (po); Draft until NS-40 is merged, so the option pattern is settled by the first PR.
