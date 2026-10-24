# NS-72: Presets menu: save, load and delete

Status: Ready
Depends on: NS-36, NS-15
Area: app

## Why
Use the preset store from the window: save the rules I typed, load them later, delete the ones I no longer want. (Part b
of the original NS-36.)

## Acceptance criteria
A `Presets` menu on a menu bar at the top of the window (this story adds the bar). Logic (name rules, menu model, which
items are enabled, every message text) in `NameShift.Ui` with tests on net10.0 and net48; the menu and the dialog are thin
controls in `NameShift.App`; every visible text comes from `Strings`; the store is read and written off the UI thread
(`Task.Run`, the result applied after `await`); routine feedback goes to the status bar (`MainForm.ShowStatus`), never a
`MessageBox`. Starting point (merged NS-36): `PresetStore` (`Save(preset, overwrite)`, `List()`, `Load(name)`,
`Delete(name)`), `Preset.FromPanels`, `RulePanelHostModel.ApplyPreset`, `PresetStore.DefaultFolder`.

Names:
1. Given a name with white space around it (`Photos `, ` Photos`), then `PresetStore` trims it: `Save` writes the trimmed
   name inside the file, and two names equal after trimming and ignoring case are the same preset (so `Photos` and
   `Photos ` cannot both exist). `Load` and `Delete` trim the name they are given, and they compare trimmed names on both
   sides, so a file written by hand with `"name": "Photos "` is still found by `Photos`. Inner spaces stay as typed. Tests in
   the store's tests: save `Photos` then `Photos ` with `overwrite` false gives `Exists`; with `overwrite` true there is one
   file; the hand-written file case; a name that is only white space is `InvalidName`, as today.
2. `PresetNames.Check(text)` (Ui) returns the trimmed name and a verdict: `Ok`, `Empty` (empty or white space) or `TooLong`
   (more than 100 characters after trimming). A test for each, with `"  "`, `"a"`, 100 and 101 characters, and a name made of
   `: / ? "` (accepted: the store makes a safe file name, NS-36:9).

Menu:
3. Given the menu bar, then `Presets` (Alt key P, letters of the items below are their own) has, in this order, `Save
   preset...`, `Load preset` (a sub-menu) and `Delete preset` (a sub-menu). `Save preset...` is enabled only when a panel is
   enabled or not at default (`RulePanelHostModel.CanResetAny`): a preset of nothing is a mistake, and Reset is the way to a
   blank state. `Load preset` and `Delete preset` list one item per saved preset in the store's order (natural, ignoring
   case), and both read `No saved presets` (disabled) when there is none. A Ui test builds the menu model from a fake list
   and checks the items, their order, their enabled states and the texts.
4. Given a preset name with `&` (`Fish & Chips`), then the item shows `Fish && Chips` in the menu (a test of the item text:
   `&` doubled), and the status messages show the name as typed. A name is never given an Alt key.
5. Given the menu opens (`DropDownOpening`), then the store is read once in the background; until it answers the two lists
   show one disabled item with the text `Strings.Loading`, then the presets (the items are replaced in the open menu). The store is never read
   at start-up. When `PresetListing` says some files could not be read, the status bar says `N presets could not be read`
   (one: `1 preset could not be read`), once per opening, and they are not offered. A folder that is missing or unreachable
   lists as empty, without a message (NS-36:11).

Save:
6. Given `Save preset...`, then a small modal dialog (`PresetNameDialog`, built in code, centred on the window) asks for the
   name: label `Preset name:` with its Alt key, a text box limited to 100 characters, pre-filled with the last loaded or
   saved name of this run (empty at first, not kept between runs: NS-37 does not remember it), `OK` and `Cancel`. `OK` is
   disabled while `PresetNames.Check` says `Empty` or `TooLong` (a pasted longer text is cut by the limit to 100 characters, so
   `TooLong` is a guard for other callers); Enter confirms, Escape cancels; the dialog is keyboard-operable and every
   control has an accessible name from `Strings`.
7. Given OK with a name nobody uses, then `Preset.FromPanels(name, RuleHost.Panels)` is saved with `overwrite` false on the
   pool and the status bar says `Preset "X" saved.` (X is the trimmed name).
8. Given the name is already used (the store answers `Exists`, ignoring case and white space around), then the user is asked
   `Replace the preset "X"?` (a Yes/No question, No is the default and Escape means No); No writes nothing and the status
   bar stays quiet; Yes saves with `overwrite` true and says `Preset "X" saved.`. The question is not feedback; results and
   errors never use a box. One Yes/No helper serves both questions and the Rename confirmation: today
   `ActionButtons.Confirm` is private, asked only above 500 names, and defaults to Yes; this story extracts it into a
   shared helper (a `MainForm` method or a `Func<...>` passed in) that takes the default button and the texts from
   `Strings`. Rename keeps its behaviour (default Yes, same text); NS-72 passes No as the default. Escape is No.
9. Given the store fails (read-only folder, disk full, share gone), then the status bar says `The preset could not be saved:
   <system message>` and nothing else happens (the old file is intact, NS-36:8).

Load:
10. Given an item of `Load preset`, then the preset is read on the pool and, back on the UI thread, applied with
    `RuleHost.ApplyPreset` (the panels equal the preset exactly; one preview run, NS-36:5); the status bar says `Preset "X"
    loaded.`; nothing is renamed and the selection, the folder and the filters are untouched. The name becomes the dialog's
    pre-fill. If the window was closed while the preset was read (`IsDisposed`), nothing is applied. If a Rename or Revert started while the preset was being read (`Actions.IsBusy`), it is not applied and the
    status bar says `The preset was not loaded: a rename is running.`.
11. Given the file vanished or changed since the list was read (`Load` answers not found), then the status bar says `The
    preset "X" was not found.` and the panels stay as they were.

Delete:
12. Given an item of `Delete preset`, then the user is asked `Delete the preset "X"?` (the shared Yes/No helper of criterion 8: No default, Escape is No); Yes
    deletes it on the pool and the status bar says `Preset "X" deleted.`; No does nothing. A preset that is not found says
    `The preset "X" was not found.`; a failure says `The preset could not be deleted: <system message>`. Only the store
    deletes, by name (NS-36:12).

Window:
13. Given a Rename or Revert is running (`MainActions.IsBusy`), then the whole `Presets` menu is disabled (the menu bar is not
    inside the split the window already locks, so `MainForm.OnActionsChanged` disables it), and it is enabled again with the
    window.
14. Alt keys: the menu title's letter is not the letter of any rule panel title (R, C, M, X, E, G, V, D, F, A, N today), and
    the letters of the three items differ from one another. A Ui test, `WindowMnemonicTests`, lists the panel titles, the
    menu title and the items' strings and fails on a repeated title letter or a repeated item letter (repeats between a panel
    title and a label inside another panel stay allowed, as `MnemonicCheck` does). NS-70 adds its row's strings to the same
    test; whichever story merges first creates it.
15. Given the layout, then the bar is a `MenuStrip` docked at the top of the window, `MainForm` only composes and wires it, the
    path bar and the rest keep their sizes, and the window's minimum size still holds at 150 % and 200 % (by hand).
16. By hand, listed in the PR: save, load, replace (Yes and No), delete (Yes and No), a name with `: / ? &`, a name of 100
    characters, a read-only presets folder, Alt+P and the item keys, 150 % and 200 % scale, a screen reader reads the menu and
    the dialog.

## Out of scope
- Remembering the last settings (NS-37), import and export, renaming a preset (save it again and delete the old one),
  filters in presets, a `Reset all` menu item (the Reset button already clears every panel and, with NS-70, the filters).
- The command line (NS-69).

## Decisions
- A menu or buttons? **Decision:** a menu: presets are used now and then, and the three action buttons stay uncluttered (po,
  2026-10-24).
- Overwrite and delete confirmations? **Decision:** both ask, since each destroys something the user made; they are Yes/No
  questions, not feedback, so they share one extracted Yes/No helper with the Rename confirmation (default No here, Yes there); every result and error goes to the status bar (po,
  2026-10-24).
- Names with white space around? **Decision:** trimmed in the store, so the window, the command line (NS-69) and a
  hand-edited file all agree and `Photos` and `Photos ` cannot both exist (from the NS-36 review, PR #65) (po, 2026-10-24).
- Save with nothing set? **Decision:** disabled: a preset with no rule is a mistake and Reset gives the blank state (po,
  2026-10-24).
- Read the store when the menu opens or keep a list? **Decision:** read when it opens, in the background, as already
  decided: no stale list, nothing read at start-up, and a roaming folder that is slow cannot delay the window (po,
  2026-10-24).
- A `Reset all` item here? **Decision:** no: the Reset button is the one place that clears everything; a second control for
  the same thing would be clutter (po, 2026-10-24).

## Log
- 2026-10-24: split out of NS-36 (po); Draft until the store (NS-36) is merged.
- 2026-10-24: from the review of NS-36 (PR #65): trim preset names, so "Photos" and "Photos " cannot both exist
  (tech-lead).
- 2026-10-24: NS-36 merged. Criteria re-checked against `PresetStore`, `ApplyPreset` and `MainForm`; trimming moved into the
  store, Loading state, busy handling, Alt-key test, status-bar rules made exact; set Ready (po).
