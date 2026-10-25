# NS-84: Remember which panels are collapsed

Status: Done
Depends on: NS-83, NS-37
Area: app

## Why
The panels come back collapsed or open as the user left them.

## Acceptance criteria
Logic in `NameShift.Ui` with tests on net10.0 and net48 against a temp folder. It extends the `settings.json` of NS-37
(same store, same atomic write, same lenient read); no new file and no new package.
1. `settings.json` gets one more member, `panels`: an object from the panel's stage name (`RuleStage` enum name, for
   example `"Remove"`) to a string `"collapsed"` or `"open"`, written for every panel at close. Version stays 1 (an
   older reader ignores the member; a missing one means "first run").
2. Reading is lenient like every other value of NS-37:2: a missing `panels`, a wrong type, an unknown stage name or an
   unknown state text is ignored on its own and the other entries still load. A panel with no entry, or no file at all
   (the first run), uses the default of NS-83:1: collapsed when its check box is off, open when on. Tests: a round trip of
   all panels; a missing member; an entry with a wrong value; an unknown stage; a file of a newer version (defaults, as
   NS-37:2). How: the in-box `DataContractJsonSerializer` fails a whole object on one wrong-typed value and writes a
   dictionary as key/value pairs, so `panels` is not a typed dictionary member. As NS-37:2 says for the numbers, it is
   read with a separate tolerant parse (a manual read of the `panels` object, or a string-keyed, string-valued map with
   `UseSimpleDictionaryFormat` read on its own, so a bad `panels` cannot spoil the window values), and written as a plain
   `"Stage": "collapsed"` object. Tests: `panels` of a wrong type (a number, an array); one entry with a number value
   while the others load; a bad `panels` while `window` and `mainSplit` still load.
3. Given saved states, then `PanelCollapseModel` (NS-83) is built from them, after the remembered rules are applied
   (NS-37:3), so a saved state wins over the check box default; a saved collapsed panel whose rules come back enabled
   stays collapsed (the saved state wins), and ticking it opens it (NS-83:3). Test: saved `collapsed` plus an enabled
   panel gives collapsed; no saved entry plus an enabled panel gives open.
4. Given the window closes, then the states are written with the rest of `settings.json` under the same bounded wait
   (NS-37:4). A close refused because a Rename or Revert runs saves nothing.
5. The first run is a start with no `panels` entry: the file is written at the first close and from then on the states are
   the user's. Reset (NS-15) does not clear them. Presets never carry them (NS-83).

## Out of scope
- Any other window layout; a "reset layout" command; sync between machines.

## Decisions
- Same file as the window facts? **Decision:** yes: it is window layout, so it goes with `settings.json` in
  `%LOCALAPPDATA%`, not with the rules in the preset format (po, 2026-10-24).
- Saved collapsed but rules come back on? **Decision:** the saved state wins at start; any later tick opens it. The user
  chose to collapse it, and the active look stays visible on a collapsed title (po, 2026-10-24).
- Split from NS-83? **Decision:** yes: NS-83 is useful alone (the row and the height), and this needs NS-37's store,
  which is not built (po, 2026-10-24).

## Log
- 2026-10-24: created; David asked for the state to be remembered with the settings. Depends on NS-37 for the store and
  on NS-83 for the model. Set Ready, to be built after both (po).
- 2026-10-25: In Progress (builder).
- 2026-10-25: In Review (builder), PR #88.
- 2026-10-25: Done (tech-lead), PR #88 approved and squash-merged; review nits folded: a stage key must equal a
  `RuleStage` name exactly (ordinal lookup in `Enum.GetNames`: `" Remove"` and `"+5"` are skipped, tested); for a
  repeated key the first valid entry wins (documented and tested); the `using` order and the long lines of this PR
  wrapped. Hand checks for David (PR #88): collapse some panels, close, reopen: they come back as left; a panel saved
  collapsed whose rules come back ticked stays collapsed with the active look and opens when ticked again (this also
  checks that `MainForm` builds the collapse model after `ApplyPreset`); first run with no `panels`: unticked panels
  collapsed; a close refused during a Rename saves nothing; Reset, close, reopen: the states stay.
