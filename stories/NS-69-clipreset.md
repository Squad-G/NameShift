# NS-69: Command line: use a saved preset

Status: Ready
Depends on: NS-38, NS-36, NS-43, NS-72, NS-42, NS-39, NS-40
Area: app

## Why
Apply a preset saved in the window from a script, and save one from the command line. (Split from NS-38.) Last of the
CLI build order NS-42, NS-39, NS-40, NS-69: it extends the parser, the help and the `CommandRunner` constructor after the
others. NS-72 must be merged first because the store trims names there (NS-72:1), which this story relies on (the store
of NS-36 does not trim yet). NS-40 must be merged first because its rule options are built as panel models, which
criterion 5 reuses.

## Acceptance criteria
Code in `src/NameShift.Cmd` and `src/NameShift.Cmd.Exe`, tests in `tests/NameShift.Cmd.Tests` (net48 for the exe), the one
small factory in `NameShift.Ui`. Messages in the command line's `Strings`. Two kinds of error, as today: a usage error
from the parser (blank name, missing value, `--overwrite` alone, no rule) has the NS-43:4 shape (message, `Strings.Usage`,
`Strings.HelpPointer`, exit 1); an input error found by the runner (preset not found, no rules in it, name taken) is its
message alone on the error stream, exit 1, like `FolderNotFound`.

Preset use:
1. Given `NameShiftCmd apply --preset "Photos" "D:\Pics"` (the order of options and folder is free, as today), then the
   saved preset's rules are applied through the same planner, checks, executor and journal as NS-38 (files only, the default
   filter, `--dry-run` supported). The preset is read from `PresetStore` (default folder `%APPDATA%\NameShift\presets`),
   by name, ignoring case and white space around (NS-72:1).
2. Given a new `Ui` factory, `RulePanelModels.CreateAll()` (the eleven panel models of `App/RulePanels.Create`, which is changed
   to take its models from it and to build each control by the model's type, throwing on a type it does not know, so a new
   panel cannot be added to one side only), then the command line builds its rules the way the window does: a headless
   `RulePanelHostModel` over the factory's models, `ApplyPreset(preset)`, then `Rules`. No test project can see `NameShift.App`
   (WinForms), so the guard is in `NameShift.Ui.Tests`: every non-abstract `RulePanelModel` subclass of the Ui assembly appears
   exactly once in `CreateAll()`, with one distinct `Stage` each, in the window's order. By hand, in the PR: the window still
   shows the same eleven panels in the same order.
3. Given the same preset and the same files, then the command line and the window give identical new names: a test runs both
   (headless `RulePanelHostModel` + `RenamePlanner`, and `CommandRunner` with `--dry-run`) on one fixture of at least six
   files, a preset with three panels (Replace, Numbering, Case), and compares the `old -> new` lines.
4. Given a preset name that is not saved, then nothing is renamed, nothing is listed, and the one-line message
   `Preset "X" was not found. Saved presets: A, B, C.` (or `There are no saved presets.`) goes to the error stream, exit 1.
   Given the presets folder is missing or cannot be read, it counts as no saved presets. Given the preset exists but no
   panel in it is enabled (it has no rule), the message is `The preset "X" has no rules.`, exit 1.
5. Given `--preset` with rule options (`--replace`, `--case`, and whatever NS-40, NS-75 and NS-76 add), then each option's rule
   is exactly what the option alone would build (the window's defaults for what the option does not say, never the
   preset's value for it) and replaces that panel's entry of the preset; every other panel of the preset stays. Mechanism:
   every rule option of the parser fills a panel model (NS-40's shape; `--replace` and `--case` are moved to models in this
   story if NS-40 left them as fields), and for each panel the options touched, the host's panel of the same `Stage` is
   `Reset()`, given the option model's `SaveSettings()` through `LoadSettings`, and `Enabled = true`, after `ApplyPreset`. A
   test: a preset with Replace (`a` to `b`, match case on) and Case upper, run with `--replace x y`: the Replace rule is `x` to
   `y` with the default (ignore case), and Case upper still applies. `--preset` alone satisfies the "at least one rule" check
   (`Strings.NoRule` is already generic since NS-40).
6. Given a preset whose rules produce rule errors or conflicts, then the same messages and exit code 2 as NS-38; the preset
   file is never changed by `apply`.

Saving:
7. Given `--save-preset NAME` with at least one rule (from options, or from `--preset` plus options), then the rules are
   saved in the shared preset format (`Preset.FromPanels(NAME, models)` over the headless host, so what is saved is what
   runs) after the arguments, the folder and any `--preset` are validated, and before the listing; the output line is
   `Preset "NAME" saved.` (new string `PresetSaved`) on the output stream; the apply (or the dry run) then goes on as
   usual. `--dry-run` does not stop the save: a preset is not a file change. `--quiet` (NS-39) hides the saved line like
   any success line; `--verbose` prints it; the other outputs are NS-39's. The window reads it back with the same panels
   (a test: save from the command line, load with `RulePanelHostModel.ApplyPreset`, equal rules).
8. Given `NAME` is already used (ignoring case and white space), then nothing is saved or renamed and the message
   `A preset named "NAME" exists. Add --overwrite to replace it.` goes to the error stream, exit 1; with `--overwrite`
   it is replaced. `--overwrite` without `--save-preset` is a usage error. NAME is checked with NS-72's
   `PresetNames.Check`, as in the window: `Empty` (blank) and `TooLong` (over 100 characters after trimming) are usage
   errors, and the trimmed name is what is saved and printed. A save that fails (read-only folder, disk full) prints
   `The preset could not be saved: <system message>`, renames nothing, exit 3 (nothing was renamed, NS-43:8).
9. Given `--save-preset` with no rule at all, then it is the usual "no rule" usage error and nothing is saved.
10. Given `--preset` or `--save-preset` is the last word with no value, or its value is an option word, then it is a usage error
    (a value that is an option word means a value was left out, as NS-38 does for `--replace`).

Messages (new keys of the command line's `Strings` unless named as existing; `{0}` is the name as trimmed):
- `PresetNotFound` = `Preset "{0}" was not found.`, followed on the same line, after one space, by `SavedPresets` =
  `Saved presets: {0}.` (the names in the store's order, joined by `, `) or `NoSavedPresets` =
  `There are no saved presets.`
- `PresetHasNoRules` = `The preset "{0}" has no rules.`
- `PresetSaved` = `Preset "{0}" saved.`
- `PresetExists` = `A preset named "{0}" exists. Add --overwrite to replace it.`
- `PresetSaveFailed` = `The preset could not be saved: {0}` (`{0}` is the system message).
- Usage errors (with `Strings.Usage` and `Strings.HelpPointer`): `PresetNameBlank` = `--save-preset needs a name.`,
  `PresetNameTooLong` = `A preset name has at most 100 characters.`, `OverwriteNeedsSave` =
  `--overwrite needs --save-preset.`; a missing value of `--preset` or `--save-preset` is NS-40's `OptionNeedsValue`
  (`{0} needs a value.`).

Help and tests of the words:
11. The new words (`--preset`, `--save-preset`, `--overwrite`) are added to `CommandLineParser.ApplyOptionWords` (so
    `Words` and the option-word guard read them), each with its own line in `HelpApply` (`--preset <name>`,
    `--save-preset <name>`, `--overwrite`), within 79 ASCII columns, and NS-43's `EveryWord_IsInTheHelp`,
    `HelpLines_AtMost79_AsciiOnly` and `HelpTexts_AreInTheComposedHelps` pass;
    `CommandLineParserTests.Words_AreTheDispatchedWords` (which pins the exact list) is updated to the new list.
    `HelpApply` keeps its one example line (NS-43's `ApplyHelp_HasItsUsageOptionsAndOneExample` asserts exactly one line
    that starts with `  NameShiftCmd apply ` and holds `--dry-run`); the preset example
    `  NameShiftCmd apply "D:\Pics" --preset Photos --dry-run` goes in the Examples of `HelpMain` (the main help only).
    The usage lines are generic (NS-42) and are not edited.
12. The runner takes the `PresetStore` as a constructor parameter, last (tests give one over a temp folder: `TestSupport` and
    the direct `new CommandRunner(...)` calls in `CommandRunnerTests` and `CommandRunnerDiskFaultTests` are updated, and the
    null-argument test gets a row); `ForDisk` takes a new `presetsFolder` parameter and makes `new PresetStore(presetsFolder)`.
    `Program.cs` keeps calling `ForDisk` (the source scan still finds `CommandRunner.ForDisk` and
    `JournaledRenamer.DefaultHistoryRoot`), passes `PresetStore.DefaultFolder`, and the version stays as NS-43 has it. No test
    touches the real `%APPDATA%` (the disk tests pass a temp presets folder). `NameShift.Cmd` references the store only
    through `Save` and `Load`/`List` (no `Delete(`: `CmdSourceScanTests` bans it in the Cmd sources).

## Out of scope
- Importing or exporting presets; deleting or listing presets from the command line (the window does that); filters in
  presets; rule options beyond those other stories add (NS-40, NS-75, NS-76); the `--quiet` and `--verbose` options
  themselves (NS-39; only the saved line is decided here).

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
- 2026-10-24: NS-43 merged. Re-checked against its code: the one-example test moves the preset example to the main help, the
  factory guard moves to `Ui.Tests` (no test sees the App), the option-over-preset mechanism uses `Reset`, `LoadSettings` and
  `Enabled`, `ForDisk` gets a `presetsFolder`, usage errors versus runner errors told apart, `--quiet` and the saved line;
  depends on NS-42, NS-39 and NS-40 for the order and on NS-72 for the trimmed names; set Ready, last of NS-42, NS-39,
  NS-40, NS-69, to be started only after NS-72 and NS-40 are merged (po).
