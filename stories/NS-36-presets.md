# NS-36: Presets: format and store

Status: Ready
Depends on: NS-12, NS-55, NS-30, NS-31, NS-59
Area: app

## Why
Reuse a set of rules. This story is the saved format and the store, in `NameShift.Ui` with tests and no window change; the
menu is NS-72. The command line (NS-69) reads the same format. (Part a of the original NS-36.)

## Acceptance criteria
Names are indicative. Code is in `NameShift.Ui` (netstandard2.0, no WinForms, no new package: the in-box
`DataContractJsonSerializer`, as the journal, NS-9); tests on net10.0 and net48 against real temp folders; text in
`Strings.resx`.

Settings of a panel:
1. Every panel model (`IRulePanelModel` implementations) provides `SaveSettings()`, a flat map of setting name to text
   (the property's name; bool as `true`/`false`, numbers invariant, enums by name, text as is), and `LoadSettings(map)`.
   `Enabled` is not in the map (it is stored beside it). Loading a map then reading the model back gives the same settings
   (a round-trip test for each of the eleven panel models, with every setting at a non-default value).
2. Given a map with an unknown key, a missing key, or a value the model cannot take (`Mode` = `sideways`, a number out of
   range, text where a number is wanted), then the known good keys are applied, every other setting takes its default, and
   nothing throws.
3. Given a rule or panel story adds a setting, then it must be saved: a test finds, by reflection, every public property
   with a public setter on every panel model and fails when its name is missing from `SaveSettings()` of a model with all
   settings set to non-default values (so a new setting cannot be forgotten, the original NS-36:7).

The preset (`Preset`: name, panels):
4. A preset is `{ "version": 1, "name": "Photos", "panels": [ { "stage": "Case", "enabled": true, "settings": { ... } } ] }`,
   UTF-8 without BOM, indented, readable, one entry per panel that is enabled or not at default; the stage is the key
   (`RuleStage` name). `Preset.FromPanels(name, models)` builds it; `Apply(models)` sets the panels.
5. Given `Apply`, then every panel in the preset gets its settings and its `Enabled`, and every panel not in it is reset to
   default and disabled, so the panels equal the preset exactly; and the preview recomputes once (a test with `PreviewModel`
   and its test delay counts one run, not one per panel).
6. Given a file with another `version`, an unknown stage, a missing field or extra fields, then a version above 1 reads
   as `UnsupportedVersion` (listed, not loaded); otherwise the known fields load and the rest is ignored. Corrupt or empty
   JSON, or a missing name, reads as `Unreadable`. Neither throws.
7. Given the filters (NS-33) or the selected folder, then they are not part of a preset (a preset is rules only).

The store (`PresetStore(folder)`; default folder `%APPDATA%\NameShift\presets`, only computed, created on the first save):
8. `Save(preset, overwrite)`: given a name not used, then the file is written; given a name already used (compared ignoring
   case, as Windows names are) and `overwrite` false, then nothing is written and the result is `Exists`; with `overwrite`
   true the file is replaced. The write goes to a temporary file in the same folder, is flushed, then moved or replaced, so a
   reader sees the old file or the new one, never half of one.
9. Given a name with characters Windows refuses (`Photos: 2024/raw?`) or a reserved name (`CON`), a trailing dot or space,
   an emoji, or 300 characters, then it is stored under a safe file name (invalid characters become `_`, reserved names and
   trailing dots/spaces are fixed, at most 100 characters, always ending `.json`), and `List()` shows the typed name from
   inside the file. Two names with the same safe file name (`a:b` and `a_b`) get different files (` (2)` added), neither
   overwriting the other.
10. Given an empty or white-space name, then `Save` refuses it (`InvalidName`) and writes nothing.
11. `List()` returns the presets sorted by name (natural, ignoring case) with their file path, plus the count of files it
    could not use (`Unreadable`, `UnsupportedVersion`); those files are never changed or deleted. A missing folder lists as
    empty and is not created by a read.
12. `Load(name)` returns the preset; an unknown name is `NotFound`. `Delete(name)` removes only that preset's file (never
    another file, never a folder); the confirmation is the window's job (NS-72). A failure (read-only, in use, access
    denied) returns a result with the system message and leaves the store as it was; nothing throws.
13. Given 200 presets, then `List()` finishes in under 1 second (a loose guard).

## Out of scope
- The menu and dialogs (NS-72); remembering the last settings (NS-37); filters in presets; sharing or importing presets
  from the web; a preset for the command line options (NS-69).

## Decisions
- Format? **Decision:** JSON with a `version` field, because it is readable and needs no dependency (po, 2026-10-18).
- Timing? **Decided by David (2026-10-24):** build presets and settings after the current batch, in two parts: the format and
  store with tests (this story), then the window menu (NS-72).
- Flat map of text per panel? **Decision:** yes, so a new setting is one line in its model and an older or newer preset
  still loads (unknown keys ignored, missing keys default); no per-rule serialiser classes (po, 2026-10-24).
- Roaming or local folder? **Decision:** `%APPDATA%` (roaming) as already written: presets are small and the user's own;
  the journal stays local (po, 2026-10-24).
- Panels not in the preset when loading? **Decision:** reset and disabled, so loading a preset gives exactly that preset
  and never a mix with what was typed before (po, 2026-10-24).
- Filters in presets? **Decision:** no for now: a preset is the rules, and the files are chosen each time (po, 2026-10-24).

## Log
- 2026-10-18: created (po).
- 2026-10-24: David chose the build order; split into format and store (this story, now Ready) and the menu (NS-72);
  criteria made testable (po).
