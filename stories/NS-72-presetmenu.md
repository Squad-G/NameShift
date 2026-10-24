# NS-72: Presets menu: save, load and delete

Status: Draft
Depends on: NS-36, NS-15
Area: app

## Why
Use the preset store from the window. (Part b of the original NS-36.) Set Ready when NS-36 is merged.

## Acceptance criteria
A `Presets` menu on a menu bar at the top of the window (the bar is added by this story if no story has added it yet;
NS-37 later adds `Reset all`). Logic (name rules, messages, which items are enabled, the result texts) in `NameShift.Ui`
with tests; the menu and dialogs are thin; every text from `Strings`; disk work off the UI thread.
1. Given `Presets`, then it has `Save preset...`, `Load preset` (a sub-menu with one item per saved preset, sorted as the
   store lists them; disabled with the text `No saved presets` when there are none) and `Delete preset` (the same list).
   A Ui test builds the menu model from a fake store and checks the items, order and enabled states.
2. Given `Save preset...`, then a small dialog asks for a name, pre-filled with the last loaded or saved name (empty at
   first); OK is disabled while the name is empty or white space; Enter confirms, Escape cancels.
3. Given the name is already used (ignoring case), then the user is asked `Replace the preset "X"?` (Yes/No, No default);
   No writes nothing; Yes overwrites (the store's `Exists` result drives it, NS-36:8).
4. Given Load, then the panels get exactly the preset (NS-36:5), the status bar says `Preset "X" loaded.`, and nothing is
   renamed. Given an unreadable or newer-version preset, then it is shown in the list's count as `N presets could not be
   read` in the status bar when the menu opens, and is not offered.
5. Given Delete, then the user is asked `Delete the preset "X"?` (Yes/No, No default); Yes removes it and the status bar says
   `Preset "X" deleted.`.
6. Given any store failure, then the status bar says `The preset could not be saved: <reason>` (or loaded/deleted) with the
   system message, and no dialog blocks (routine feedback goes to the status bar).
7. Given a rename or revert is running (NS-15's lock), then the Presets menu is disabled; the store is read when the menu
   opens, never at start-up.
8. The menu and items have Alt keys that clash with no panel, filter row or button (the mnemonic test of NS-70, or a new one
   if NS-70 is not merged); the dialog is keyboard-operable and has accessible names.
9. By hand: save, load, replace, delete, a name with `: / ?`, 150 % and 200 % scale, a screen reader reads the items.

## Out of scope
- Remembering the last settings (NS-37), import/export, renaming a preset (save it again and delete the old one).

## Decisions
- A menu or buttons? **Decision:** a menu: presets are used now and then, and the three action buttons stay uncluttered (po,
  2026-10-24).
- Overwrite and delete confirmations? **Decision:** both ask, since each destroys something the user made (po, 2026-10-24).

## Log
- 2026-10-24: split out of NS-36 (po); Draft until the store (NS-36) is merged.
- 2026-10-24: from the review of NS-36 (PR #65): trim preset names, so "Photos" and "Photos " cannot both exist
  (tech-lead).
