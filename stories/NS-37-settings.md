# NS-37: Remember last settings

Status: Ready
Depends on: NS-36, NS-72
Area: app

## Why
The app opens as I left it: same place and size, same folder, same rules. Build it after NS-72 (David's order: presets
first; both change `MainForm`, so they are not built at the same time).

## Acceptance criteria
Logic in `NameShift.Ui` with tests on net10.0 and net48 against real temp folders; `MainForm` and `Program` only read and
apply it. No new package: the in-box `DataContractJsonSerializer`, the atomic write and the file seam of the preset store
(NS-36) are reused. Starting point: `Preset.FromPanels`, `PresetJson`, `RulePanelHostModel.ApplyPreset`, `PresetStore`'s
file access (`IPresetFiles`), `FolderNavigator`, `OpenTarget`, `OpenSequence`.

Files (`AppSettingsStore(folder)`, default `%LOCALAPPDATA%\NameShift`, only computed, created on the first save):
1. `settings.json`: `{ "version": 1, "window": { "left", "top", "width", "height", "maximized" }, "mainSplit", "rightSplit",
   "lastFolder" }`, UTF-8 without BOM, indented. Sizes and splitter values are in 96-DPI logical pixels, the position in
   screen pixels. `last-rules.json`: the rules in the preset format (NS-36:4) under the fixed name `(last used)`, written
   with `Preset.FromPanels` and `PresetJson`, so a new panel setting is remembered the moment it is in a preset. Writes
   are atomic (temporary file, then move: a failing write leaves the old file and no temporary file; a test with the file
   seam that fails), never throw, and a failure is silent (closing the window is not a place for a message).
2. Reading is lenient and never throws: a missing file gives defaults; empty, corrupt, over 1 MB or newer-version content gives
   defaults for that file and the file is left alone until the next save replaces it; inside a good file a missing or
   invalid value (a text where a number goes, a negative size) defaults on its own and the others still load. Tests: each
   case, and a round trip of every value.
3. Given a remembered preset in `last-rules.json`, then at start `RuleHost.ApplyPreset` applies it before the first listing,
   so the panels hold the values and the enabled boxes as left (one `RulesChanged`, no preview yet); a file with no panels,
   or an unreadable one, leaves every panel at default.

Window:
4. Given the window closes (any way that ends the process normally: the close box, Alt+F4, Windows shutting down), then both
   files are written. The work runs on the pool and the close waits at most 2 seconds for it, so a dead disk never keeps
   the window open. A close that is refused because a Rename or Revert runs (NS-15) writes nothing. Only the last window
   to close wins when two run at once; there is no lock.
5. Given a saved placement, then `WindowPlacement.Fit(saved, screens, minimumSize)` (pure, plain integers, in `NameShift.Ui`)
   decides: the window opens at the saved place and size when at least 100 px wide of its top 40 px strip lies inside one
   screen's working area; otherwise (a screen that is gone, a position far outside) the answer is "no placement" and the
   window opens centred on the primary screen at the default size (today's behaviour). The size is never below the minimum
   size and never above the working area of the screen it lands on. A maximised window comes back maximised, at its
   restore bounds underneath; the window never starts minimised. Tests: on screen, partly off screen but grabbable, fully
   off screen, a second screen that is gone, a size too small, a size larger than the screen, two screens with negative
   coordinates (a screen left of the primary), maximised.
6. Given saved splitter values, then `MainForm.OnLoad` applies them in place of the defaults (260 and the bottom height of
   240 logical pixels), clamped to the panels' minimum sizes; a value that is missing, negative or larger than the container
   is ignored (the default is used). A Ui test of the clamp rule (`SplitterRule.Clamp`), the rest by hand.
7. Sizes are saved in logical pixels (device size divided by the window's scale) and applied with the scale of the screen the
   window lands on, so a layout saved at 150 % opens right at 100 % and the reverse (by hand, written in the PR).

Folder:
8. Given no path argument on the command line, then the remembered folder opens: it is checked with `IFolderSource.CheckFolder`
   off the UI thread, as `OpenTarget` does, and a folder that is gone, not a folder, unreachable or empty falls back to
   Documents (the usual start folder) with no message. Given a path argument or a drop, then that wins (NS-47) and the
   remembered folder is ignored. The folder saved at close is `Navigator.CurrentFolder` (nothing is saved when none was
   shown). While the check runs the window is usable (a click in the tree or a path typed cancels it, as for
   `OpenAsync`). A Ui test of the choice (a small `StartFolderChoice`): argument over remembered, remembered when it
   exists, fallback when it does not, with a fake `IFolderSource`.

What is not remembered:
9. The selection, the sort, the filter row (NS-70) and the Sub-folders box (NS-78), the Rename lock and the Presets dialog's
   last name start fresh every time. The rules (criterion 3) are the only thing of the working state that comes back.

Reset:
10. Given the remembered rules come back, then the Reset button (NS-15, and with NS-70 the filters too) is the one-click way
    to clear them; there is no `Reset all` item and no "start empty" setting. After Reset and a close, `last-rules.json` holds
    no panel and the next start is clean (a test: apply a preset, reset, save, read: no panel).

Privacy:
11. Both files stay on the machine under the user's profile; nothing is sent anywhere and no other data is kept (the folder
    path and the rule values are the user's own).

## Out of scope
- Remembering filters, the selection, the sort order or the column widths; import or export of settings; a settings dialog.
- Saving the rules while the window runs (after each change); a crash or a kill loses the changes since the last start.

## Decisions
- Remember rules or start clean? **Decision:** remember them, with Reset to clear, because repeat jobs are common (po,
  2026-10-18).
- Where? **Decision:** `%LOCALAPPDATA%` and not the roaming `%APPDATA%` of the presets: a window position and a last folder
  belong to one machine and one screen layout, a preset belongs to the user (po, 2026-10-24).
- Two files? **Decision:** yes: the rules use the preset format and its code unchanged, so there is no second serialiser
  for the panels and a new setting is remembered for free; `settings.json` holds only the window facts (po, 2026-10-24).
- `Reset all` menu item? **Decision:** dropped (NS-72 and NS-70 say the same): the Reset button already clears every panel
  and the filters; a second control for it is clutter (po, 2026-10-24).
- Filters remembered? **Decision:** no: a filter hides files, and opening the app with some files hidden by something the
  user does not see at once is the kind of surprise a renamer must avoid (po, 2026-10-24).
- Save when? **Decision:** at close only, with a 2-second bound; saving on every change would write a file per keystroke for
  a rare crash case (po, 2026-10-24).
- Why does this depend on NS-72? **Decision:** David's order (presets, then this) and the shared `MainForm` edits; no code
  of the menu is used (po, 2026-10-24).

## Log
- 2026-10-18: created (po).
- 2026-10-24: David chose to build presets first, then this; it reuses the panel `SaveSettings`/`LoadSettings` of NS-36. Stays Draft: its criteria are written when NS-36 and the menu (NS-72) are merged (po).
- 2026-10-24: NS-36 merged and NS-72 Ready: criteria written in full (files, placement rule, splitters, start folder, what
  is not remembered, Reset without a menu item); set Ready, to be built after NS-72 (po).
