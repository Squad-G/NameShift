# NS-76: Command line: RegEx, Move/Copy, Auto date and Folder name options

Status: Done
Depends on: NS-75, NS-31, NS-59
Area: app
Build after NS-75 is merged (it sets the five option rules, the cut-text helper and the panel readers this story reuses; both
change the parser, the help and `Strings.resx`).

## Why
Last batch of rule options for the command line, so every panel of the window can be used from a script. Split from the
original NS-40.

## Acceptance criteria
Code in `src/NameShift.Cmd` (one panel reader each, as NS-79 and NS-75 leave them; `ApplyPanels`; `Strings.resx`). The rules
are built by the window's own models (`RegexPanelModel`, `MoveCopyPanelModel`, `AutoDatePanelModel`,
`FolderNamePanelModel`), each given with `Enabled = true` and put in `ApplyPanels.Given`. Everything in NS-75's preamble holds
here: the option rules R1 to R5 (one flag per mode, a modifier needs its option, what the window disables is ignored, never
skip silently, the window's own text cut at `Until then`), the option shape of NS-40:7, the NS-43:4 error shape, a constant
and an `ApplyOptionWords` entry per option, no `ApplyOptions` parameter, any order, no repeats.

RegEx (the RegEx panel):
1. Given `--regex <pattern> <replacement>` (two values, as `--replace`), `--regex-ignore-case` and `--regex-extension`
   (`IncludeExtension`), then the pattern is replaced by the replacement in each base name (the extension is left alone
   unless `--regex-extension`), with the .NET replacement syntax (`$1`, `${name}`) as the window: `IMG_001.JPG` with
   `--regex "(\d+)" "[$1]"` gives `IMG_[001].JPG`. An empty replacement is valid (it deletes the match). A value that is an
   option word counts as missing: the new `RegexNeedsValues` = `--regex needs two values: the pattern and the
   replacement.`.
2. Given an empty pattern, then `EmptyOptionText` (NS-75) with `--regex`; given a pattern that does not parse (`(`), then
   `--regex: This pattern is not valid. <the parser's message>` (R5, `Strings.RegexPatternInvalid`, whose `{0}` is the
   parser's message). Given `--regex-ignore-case` or `--regex-extension` without `--regex`, then `NeedsOption` with
   `--regex`.

Move or copy (the Move or copy panel; the options keep the panel's prefix `--move-` for both modes, so there are six
words, not twelve):
3. Given `--move <n>` or `--copy <n>` (mode Move or Copy and `CountText` in one value: the first `n` characters of the base
   name), `--move-from start|end` (`Source`, default start), `--move-to start|end|position` (`Target`, default end),
   `--move-position <n>` (`PositionText`) and `--move-separator <text>` (`Separator`, default empty), then the characters
   are moved or copied, as the panel does. On `abcdef.txt`: `--move 3` gives `defabc.txt`, `--copy 3` gives
   `abcdefabc.txt`, `--move 3 --move-from end --move-to start` gives `defabc.txt`. The position and the separator
   examples are left to the parity test (the oracle).
4. Given both `--move` and `--copy`, then `OneModeOnly` with `--move or --copy`. Given any of the four modifiers without
   `--move` or `--copy`, then `NeedsOption` with `--move or --copy`. Given a count that is not a whole number from 1
   (`0`, `x`, and also empty or white space: `MoveCopyPanelModel.CountInvalid` flags a typed value only, so the parser checks
   blank itself, with a test row for `--move ""` and `--copy " "`),
   then `--move: Type a whole number from 1.` (or `--copy:`, the flag given; `Strings.MoveCopyNumberInvalid` cut). Given
   `--move-to position` with no usable `--move-position` (absent, `0`, `x`), then `--move-position: Type a whole number
   from 1.` (the same string; `PositionInvalid` is true when the position applies). Given `--move-position` and a target
   other than `position`, then it is accepted and ignored (R3). An unknown word is `BadChoice` with the new values strings
   `MoveFromValues` = `start or end` and `MoveToValues` = `start, end or position`.

Auto date (the Auto date panel):
5. Given `--date` (numbering-style switch: the panel's defaults, modified date, `yyyy-MM-dd`, prefix, separator `_`),
   `--date-source modified|created`, `--date-format <format>`, `--date-placement prefix|suffix` and `--date-separator
   <text>`, then any of them turns the rule on with the panel's defaults for the rest. A file written on 2024-03-09 14:05:07
   (local time) named `a.txt` gives `2024-03-09_a.txt` with `--date`, and `a_20240309.txt` with `--date-placement suffix
   --date-format yyyyMMdd`; `--date-source created` reads the creation date. An empty separator is valid. The photo date
   comes with NS-62, which adds `photo` to the source words and their help line; here the values are `modified` and
   `created` only (`DateSourceValues` = `modified or created`).
6. Given `--date-format ""`, then `EmptyOptionText` with `--date-format`; given a format that .NET or the rule refuses
   (`yyyy zzz`: the rule refuses time-zone letters; `yyyy%`), then `--date-format: This format is not valid. <message>`
   (R5, `Strings.AutoDateFormatInvalid`). A format that gives a name Windows refuses (`yyyy:MM`) is not a usage error: the
   invalid-name check of NS-5 reports it, exit 2, nothing renamed.

Append folder name (the Append folder name panel; the switch is `--folder-name`, a switch that takes no value):
7. Given `--folder-name`, `--folder-levels <n>`, `--folder-placement prefix|suffix` and `--folder-separator <text>`, then any
   of them turns the rule on with the panel's defaults for the rest (1 level, prefix, separator `_`). Files in a folder named
   `Trip` give `Trip_a.txt` for `a.txt`, and `a_Trip.txt` with `--folder-placement suffix`. A folder with no name (a drive
   root) is left to the rule (the parity test uses a temp folder, never a drive root). Per-folder names for sub-folders come
   with NS-41.
8. Given a level count that is not a whole number from 1 (`0`, `x`, empty), then `--folder-levels: Type a whole number from
   1.` (`Strings.FolderNameLevelsInvalid` cut; the model marks an empty count too, as the panel has a default). An unknown
   placement is `BadChoice` with `PlacementValues` = `prefix or suffix` (shared by `--date-placement` and
   `--folder-placement`).

Same as the window, and the rest:
9. Given each of criteria 1, 3, 5 and 7 (a table of cases covering every option, each switch alone), then a parity test
   (as NS-38:4, NS-40:8) builds the same fixture through `PreviewModel` with the panel models set to the same values and
   through the CLI, and compares every new name. The date fixture sets file times with `File.SetLastWriteTime` and
   `File.SetCreationTime` in local time, so the test does not depend on the time zone; the Move table has a Unicode name
   (an e with an acute accent in `cafe.txt`) and a surrogate pair (an emoji before `abc.txt`; write non-ASCII as `\u` escapes
   in the test); the Regex table has a group reference, a named group, an empty replacement and `--regex-extension`.
10. Given rules from several panels in one line (`--regex a b --move 1 --date --folder-name --suffix _x`), then the names
    are the same in any order of the options (a test shuffles three orders); every option of this story counts as "a rule
    option given" for the check of NS-38. A test also puts all eleven panels in one line (with NS-40 and NS-75's options)
    and checks that the rules reach the pipeline in the window's panel order (`RuleStage`), whatever the typing order.
11. Given the help (NS-43), then every new option word (18: `--regex`, `--regex-ignore-case`, `--regex-extension`, `--move`,
    `--copy`, `--move-from`, `--move-to`, `--move-position`, `--move-separator`, `--date`, `--date-source`, `--date-format`,
    `--date-placement`, `--date-separator`, `--folder-name`, `--folder-levels`, `--folder-placement`, `--folder-separator`) is
    listed in `HelpApply` on its own line, after NS-75's lines and before `--dry-run`, each starting with the option word and
    its values, one short sentence, at most 79 ASCII columns, at the sentence column of NS-40 and NS-75, a continuation line
    indented to it (the words with a list of values, such as `--move-to start|end|position`, take their own line and a
    continuation, as `--number-mode` does). NS-43's three help tests pass, `Words_AreTheDispatchedWords` is updated, and the
    usage lines, `NoRule` and the last help line are not edited.
12. Given the new strings, then every key resolves (`CmdStringsTests.EveryKeyResolves`) and each is used by a test that checks
    its exact text: `RegexNeedsValues`, `MoveFromValues`, `MoveToValues`, `DateSourceValues`, `PlacementValues`, and
    the shared ones of NS-75 with these options. The R5 guard of NS-75 lists the four window strings of this story.
13. Given every panel of the window now has options, then a reflection test over the `NameShift.Ui` assembly checks that each
    non-abstract `RulePanelModel` subclass is either `ReplacePanelModel` or `CasePanelModel` (built from the `--replace` and `--case`
    options as NS-69 leaves them: fields of `ApplyOptions` or models, the test follows what NS-69 did) or the type of a property of `ApplyPanels`, failing with the type's name, so a future panel cannot be added to
    the window and forgotten on the command line.

## Out of scope
- Name, Extension, Remove (NS-75); saved presets (NS-69); recursion, mask and filters (NS-41); the photo date (NS-62).
- Dates in another culture's format (the .NET format string is passed as given, with the invariant culture, as the rule
  does); a `--date-format` list of common formats.

## Decisions
- Move and copy: twelve words or six? **Decision:** six, the panel's prefix `--move-` for both modes: `--move <n>` or `--copy
  <n>` carries the mode and the count, and the other four describe where the text goes; a `--copy-to` twin would double the
  help for nothing (po, 2026-10-24).
- Switch word for Auto date and Folder name? **Decision:** `--date` and `--folder-name` are the plain switches (as `--number`),
  and any other option of the panel also turns it on; no `--date-on` pair, so a script says only what it changes (po,
  2026-10-24).
- A position with a target that does not use it? **Decision:** ignored (R3), and a target `position` with no position is an
  error: the first mirrors the disabled box, the second would silently do nothing (po, 2026-10-24).
- A date format Windows would refuse as a name? **Decision:** not a usage error: the format is valid for .NET and the planner's
  invalid-name check already refuses the result with the file named, so the command line adds no second list of bad
  characters (po, 2026-10-24).
- The completeness test (criterion 13)? **Decision:** yes: the command line and the window must never drift apart, which is the
  same reason NS-69 gets one factory of models (po, 2026-10-24).
- `--folder-name` reads as if it took a value? **Decision:** accepted: the help line says "Add the name of the folder", and
  `--folder` alone would clash with the folder argument in people's minds (po, 2026-10-24).

## Log
- 2026-10-24: created (po); Draft until NS-40 is merged.
- 2026-10-24: NS-40 approved. Criteria written from the panel models (every setting an option, the window's error text cut),
  after NS-75 settles the five option rules; set Ready, to start after NS-75 is merged (po).
- 2026-10-24: PR #75 review applied: blank `--move` count refused, Replace and Case worded as NS-69 leaves them (po).
- 2026-10-24: In Progress (builder).
- 2026-10-24: In Review (builder), PR #79.
- 2026-10-24: Done (tech-lead), PR #79 approved on re-review and squash-merged. Follow-ups (optional review nits, to be
  taken together with NS-75's reader-style nit 4 as one future `[chore]` cleanup of the readers): `NumberingReader` keeps
  its own copies of the choice reading where `OptionArgs.TakeChoice` now exists; `ReadSwitch` is duplicated in
  `AutoDateReader` and `FolderNameReader`; the repeated error/ok chain in `ReadOwn`; the `NameShift.Ui.Strings` alias.
