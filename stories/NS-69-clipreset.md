# NS-69: Command line: use a saved preset

Status: Draft
Depends on: NS-38, NS-36, NS-43, NS-72
Area: app

## Why
Apply a preset saved in the window from a script, and save one from the command line. (Split from NS-38.) Draft only
until NS-43 is merged: it turns `CommandLineParser` into a dispatcher with a word list and a help text that this story
extends, and changes the `CommandRunner` constructor and `ForDisk`. NS-72 is listed because the store trims names there
(NS-72:1), which this story relies on. Criteria are written against what NS-43 promises; re-check them against its merged
code, then set Ready.

## Acceptance criteria
Code in `src/NameShift.Cmd` and `src/NameShift.Cmd.Exe`, tests in `tests/NameShift.Cmd.Tests` (net48 for the exe), the one
small factory in `NameShift.Ui`. Messages in the command line's `Strings`; usage errors have the NS-43 shape (one line:
message, usage, help pointer, exit 1).

Preset use:
1. Given `NameShiftCmd apply --preset "Photos" "D:\Pics"` (the order of options and folder is free, as today), then the
   saved preset's rules are applied through the same planner, checks, executor and journal as NS-38 (files only, the default
   filter, `--dry-run` supported). The preset is read from `PresetStore` (default folder `%APPDATA%\NameShift\presets`),
   by name, ignoring case and white space around (NS-72:1).
2. Given a new `Ui` factory, `RulePanelModels.CreateAll()` (the eleven panel models of `RulePanels.Create`, which is changed to
   use it so a new panel cannot be added to one side only), then the command line builds its rules the way the window does:
   a headless `RulePanelHostModel` over the factory's models, `ApplyPreset(preset)`, then `Rules`. A test walks both lists
   and fails when the window and the factory differ in count or stages.
3. Given the same preset and the same files, then the command line and the window give identical new names: a test runs both
   (headless `RulePanelHostModel` + `RenamePlanner`, and `CommandRunner` with `--dry-run`) on one fixture of at least six
   files, a preset with three panels (Replace, Numbering, Case), and compares the `old -> new` lines.
4. Given a preset name that is not saved, then nothing is renamed, nothing is listed, and the one-line message
   `Preset "X" was not found. Saved presets: A, B, C.` (or `There are no saved presets.`) goes to the error stream, exit 1.
   Given the presets folder is missing or cannot be read, it counts as no saved presets. Given the preset exists but no
   panel in it is enabled (it has no rule), the message is `The preset "X" has no rules.`, exit 1.
5. Given `--preset` with rule options (`--replace`, `--case`, and whatever NS-40, NS-75 and NS-76 add), then each option's rule
   is exactly what the option alone would build (the window's defaults for what the option does not say, never the
   preset's value for it) and replaces that panel's entry of the preset; every other panel of the preset stays. A test: a
   preset with Replace (`a` to `b`, match case on) and Case upper, run with `--replace x y`: the Replace rule is `x` to `y`
   with the default (ignore case), and Case upper still applies.
6. Given a preset whose rules produce rule errors or conflicts, then the same messages and exit code 2 as NS-38; the preset
   file is never changed by `apply`.

Saving:
7. Given `--save-preset NAME` with at least one rule (from options, or from `--preset` plus options), then the rules are
   saved in the shared preset format (`Preset.FromPanels(NAME, models)` over the headless host, so what is saved is what
   runs) after the arguments, the folder and any `--preset` are validated, and before the listing; the output line is
   `Preset "NAME" saved.`; the apply (or the dry run) then goes on as usual. `--dry-run` does not stop the save: a preset
   is not a file change. The window reads it back with the same panels (a test: save from the command line, load with
   `RulePanelHostModel.ApplyPreset`, equal rules).
8. Given `NAME` is already used (ignoring case and white space), then nothing is saved or renamed and the message `A preset named
   "NAME" exists. Add --overwrite to replace it.` goes to the error stream, exit 1; with `--overwrite` it is replaced. `--overwrite`
   without `--save-preset` is a usage error. A blank NAME is a usage error. A save that fails (read-only folder, disk full)
   prints `The preset could not be saved: <system message>`, renames nothing, exit 3 (nothing was renamed, NS-43:8).
9. Given `--save-preset` with no rule at all, then it is the usual "no rule" usage error and nothing is saved.
10. Given `--preset` or `--save-preset` is the last word with no value, or its value is an option word, then it is a usage error
    (a value that is an option word means a value was left out, as NS-38 does for `--replace`).

Help and tests of the words:
11. The new words (`--preset`, `--save-preset`, `--overwrite`) are in `CommandLineParser.Words`, in the `apply` help (`HelpApply`,
    each with one line, and one example `apply D:\Pics --preset Photos --dry-run`), within 79 ASCII columns, and NS-43's
    test that every word of the list is in the help passes; the option-word guard reads the same list.
12. The runner takes the `PresetStore` as a constructor parameter (tests give one over a temp folder); `ForDisk` makes
    `new PresetStore(PresetStore.DefaultFolder)`. `Program.cs` keeps calling `ForDisk` and the version stays as NS-43 has it.
    No test touches the real `%APPDATA%`.

## Out of scope
- Importing or exporting presets; deleting or listing presets from the command line (the window does that); filters in
  presets; rule options beyond those other stories add (NS-40, NS-75, NS-76); `--quiet` and `--verbose` (NS-39).

## Decisions
- Why a separate story? **Decision:** the first CLI story (NS-38) must not wait for presets (NS-36), which are built in the
  same round; this one follows both (po, 2026-10-24).
- An option next to `--preset`? **Decision:** the option wins and replaces that rule whole, never merged with the preset's
  values: a half-merged rule is a surprise a script cannot see (po, 2026-10-24).
- Overwrite a saved preset from a script? **Decision:** refuse unless `--overwrite` is given: a preset is something the user
  made by hand, a script must not lose it by a typo, and the flag keeps it possible (po, 2026-10-24).
- Does `--save-preset` also rename? **Decision:** yes, it is `apply` with a side effect, and `--dry-run` is how to save without
  renaming; a separate verb would be a second parser path for the same rules (po, 2026-10-24).
- Same panels as the window? **Decision:** one factory in `Ui` used by both, so a preset means the same in both and a new
  panel is never missing from the command line (po, 2026-10-24).

## Log
- 2026-10-24: split out of NS-38 and NS-40 (po).
- 2026-10-24: NS-36 merged. Criteria rewritten against `PresetStore`, `ApplyPreset` and the unmerged NS-43; stays Draft until
  NS-43 is merged (the word list, the help and the `CommandRunner` constructor are its) (po).
