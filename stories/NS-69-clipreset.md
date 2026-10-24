# NS-69: Command line: use a saved preset

Status: Draft
Depends on: NS-38, NS-36
Area: app

## Why
Apply a preset saved in the window from a script. (Split from NS-38; waits for presets, NS-36.)

## Acceptance criteria
1. Given `NameShiftCmd apply --preset "Photos" "D:\Pics"`, then the saved preset's rules are applied and the files renamed
   through the same checks, executor and journal as NS-38.
2. Given the same preset and files, then the CLI and the window produce identical new names (a test runs both on one fixture).
3. Given a preset name that does not exist, then nothing is renamed, the message names the preset and lists the available
   ones, and the exit code is 1.
4. Given `--preset` with rule options, then the options are added to the preset's rules and an option for a rule replaces
   that rule's preset values (as in NS-40).
5. Given `--save-preset NAME` with rules, then they are saved in the shared preset format (NS-36).

## Out of scope
- Anything not listed above.

## Decisions
- Why a separate story? **Decision:** the first CLI story (NS-38) must not wait for presets (NS-36), which are built in
  the same round; this one follows both (po, 2026-10-24).

## Log
- 2026-10-24: split out of NS-38 and NS-40 (po).
