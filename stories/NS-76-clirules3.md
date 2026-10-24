# NS-76: Command line: RegEx, Move/Copy, Auto date and Folder name options

Status: Draft
Depends on: NS-40, NS-31, NS-59
Area: app

## Why
Last batch of rule options for the command line, so every panel of the window can be used from a script. Split from the
original NS-40.

## Acceptance criteria
To write when NS-40 is merged (po), following its pattern: one flag per setting of `RegexPanelModel`,
`MoveCopyPanelModel`, `AutoDatePanelModel` and `FolderNamePanelModel`, values refused with the model's own error, a
parity test through `PreviewModel` for every option, every option in the help (NS-43's test), any order, no repeats.
Auto date's photo-date source follows NS-62.

## Out of scope
- Name, Extension, Remove (NS-75).

## Decisions
- None yet.

## Log
- 2026-10-24: created (po); Draft until NS-40 is merged.
