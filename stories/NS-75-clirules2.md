# NS-75: Command line: Name, Extension and Remove options

Status: Done
Depends on: NS-40, NS-69, NS-79, NS-55, NS-30
Area: app
Build after NS-40, NS-69 and NS-79 are merged (all four change the parser, the help and `Strings.resx`), and before NS-76.

## Why
Second batch of rule options for the command line: change or reverse the name, change the extension, and remove text from
names, from a script. Split from the original NS-40; NS-40's pattern is followed.

## Acceptance criteria
Code in `src/NameShift.Cmd` (the panel readers of NS-79, `ApplyPanels`, `Strings.resx`, `HelpText` untouched in code). The
rules are built by the window's own models (`NamePanelModel`, `ExtensionPanelModel`, `RemovePanelModel`), each given with
`Enabled = true` and put in `ApplyPanels.Given` (so NS-69's "an option replaces its panel's entry of a preset" picks them up
with no change). Rules run in the pipeline's fixed order whatever the typing order. No constructor parameter is added to
`ApplyOptions`. Every option is built from a constant, added to `CommandLineParser.ApplyOptionWords`, and takes the shape
of NS-40:7: a value that is an option word counts as missing (`OptionNeedsValue`), a missing value is `{0} needs a value.`,
a repeat is `RepeatedOption`, any position, and a value may start with a dash. The usage-error shape is NS-43:4 (message,
`Strings.Usage`, `Strings.HelpPointer`, exit 1, nothing renamed). The five option rules below are shared with NS-76.

The five rules (the "option rules"):
- **R1 One flag per mode.** A rule with modes has one flag per mode, and giving two of them is the error `OneModeOnly` =
  `Give only one of {0}.` (`{0}` is the list of that rule's flags, a string per panel). Reason: no value word to spell, no
  ignored text.
- **R2 A modifier needs its option.** An option that does nothing without another (`--remove-whole-word` without
  `--remove-text`) is the error `NeedsOption` = `{0} needs {1}.` (`{1}` is the option, or a list `--a, --b or --c`).
- **R3 What the window disables is ignored.** A value for a field that the chosen mode turns off in the panel is accepted and
  ignored, as NS-40:6 does for `--number-pad` in letters.
- **R4 Never skip silently.** Text or a number that the window marks red, or that the rule would skip without a word, is
  refused with one line (R5), nothing renamed.
- **R5 The window's own text.** A number or a pattern that is refused gives `OptionError` = `{0}: {1}` with the window's
  string for it, minus its last sentence (`Until then ... is skipped.`). The cut is one helper that removes everything from
  the last `Until then` (new string `SkipSentenceMarker` = `Until then`, replacing NS-40's `NumberingSkippedSentence`;
  the numbering messages read as before). The two tests that name the old key are edited to the new key and helper with the
  same asserted outputs: `CmdStringsTests` (the exact text of the key, line 55) and `CommandLineParserTests` (the expected
  cut, lines 623-624). A test asserts that every window string
  the command line shows (NS-75 and NS-76 list them) contains the marker and that the cut text contains neither `Until` nor
  `skipped`.

Name (the Name panel):
1. Given `--name-fixed <text>`, `--name-empty`, `--name-reverse` (modes Fixed, Remove, Reverse; none given leaves the panel
   off), then the base name is replaced, emptied or reversed and the extension is left alone: `IMG_001.JPG` with
   `--name-fixed Trip` gives `Trip.JPG`; `abc.txt` with `--name-reverse` gives `cba.txt`. `--name-empty` is the Remove mode
   (its flag is not `--name-remove`, which would read as the Remove rule): what the planner makes of an empty base name is
   the planner's (the parity test is the oracle). `a.txt` and `b.txt` with `--name-fixed x` give the duplicate-target
   conflict, exit 2, nothing renamed (NS-38).
2. Given two of the three flags, then `OneModeOnly` with `--name-fixed, --name-empty or --name-reverse`. Given
   `--name-fixed` with a text that is empty or only white space (`NamePanelModel`/`NameRule` would skip it), then the new
   `BlankText` = `The text of {0} cannot be empty or only spaces.`.

Extension (the Extension panel):
3. Given `--ext-lower`, `--ext-upper`, `--ext-fixed <text>`, `--ext-extra <text>`, `--ext-remove` (modes Lower, Upper, Fixed,
   Extra, Remove), then: `IMG_001.JPG` with `--ext-lower` gives `IMG_001.jpg`; `a.txt` with `--ext-upper` gives `a.TXT`;
   `a.jpg` with `--ext-fixed jpeg` or `--ext-fixed .jpeg` gives `a.jpeg` (the rule trims dots and spaces at both ends);
   `a.txt` with `--ext-extra bak` gives `a.txt.bak`; `a.txt` with `--ext-remove` gives `a`; `README` (no extension) with
   `--ext-fixed md` becomes `README.md`. Folders are never touched (files only, NS-38).
4. Given two of the five flags, then `OneModeOnly` with `--ext-lower, --ext-upper, --ext-fixed, --ext-extra or
   --ext-remove`. Given `--ext-fixed` or `--ext-extra` with a text that is empty or only dots and spaces (the rule would
   skip it), then the new `BlankExtension` = `The text of {0} cannot be empty or only dots and spaces.`.

Remove (the Remove panel; the options are named after its settings, and every flag below sets the model property named):
5. Given the options `--remove-first <n>` (`FirstText`), `--remove-last <n>` (`LastText`), `--remove-from <n>` (`FromText`)
   with `--remove-to <n>` (`ToText`), `--remove-chars <text>` (`Characters`), `--remove-text <text>` (`Text`),
   `--remove-before <text>` (`CropBefore`), `--remove-after <text>` (`CropAfter`), and the switches `--remove-whole-word`,
   `--remove-match-case`, `--remove-keep-marker`, `--remove-digits`, `--remove-symbols`, `--remove-accents`,
   `--remove-high-chars`, `--remove-trim`, `--remove-leading-dots`, `--remove-double-spaces` (the ten bool settings), then
   the Remove panel's rule is built from them, in the panel's own order of parts. Examples on `IMG_001.JPG`:
   `--remove-first 3` gives `_001.JPG`, `--remove-last 1` gives `IMG_00.JPG`, `--remove-digits` gives `IMG_.JPG`,
   `--remove-text img` gives `_001.JPG` (ignoring case, the window's default), `--remove-text img --remove-match-case`
   changes nothing; on `abcdef.txt`, `--remove-from 2 --remove-to 3` gives `adef.txt` (both ends counted); on
   `Trip - a.txt`, `--remove-before " - "` gives `a.txt` and with `--remove-keep-marker` gives ` - a.txt`.
6. Given a count or a range the window would mark red, or leaves blank (the models flag a typed bad value only, so the
   parser also checks that the value is not empty or white space), then R5: `--remove-first 0`, `--remove-first x`,
   `--remove-first ""` or `--remove-first " "` gives `--remove-first: Type a whole number from 1.`
   (`Strings.RemovePositionInvalid` cut), the same for `--remove-last`;
   `--remove-from` without `--remove-to` (or the reverse), either one blank (also both blank), or From after To, gives `--remove-from and --remove-to: Type two
   whole numbers from 1, From not after To.` (`Strings.RemoveRangeInvalid` cut; the label is a new string
   `RemoveRangeOptions`). Given `--remove-chars`, `--remove-text`, `--remove-before` or `--remove-after` with an empty text
   (the rule skips it), then the new `EmptyOptionText` = `The text of {0} cannot be empty.` (a single space is a valid text).
7. Given a modifier without its option (R2), then `NeedsOption`: `--remove-whole-word` needs `--remove-text`;
   `--remove-match-case` needs `--remove-chars, --remove-text, --remove-before or --remove-after`;
   `--remove-keep-marker` needs `--remove-before or --remove-after`. With its option present, the modifier is accepted
   (R3 does not apply: the panel enables it). Because of R2 and R4 (blank counts and texts are refused in criterion 6), any
   Remove option the parser accepts builds a rule: a test asserts `BuildRule()` is never null for a model the parser
   returns, over the table of accepted cases, and that each refused blank case of criterion 6 is an error.

Same as the window, and the rest:
8. Given each of criteria 1, 3 and 5 (a table of cases covering every option, each switch alone and in a pair), then a parity
   test (as NS-38:4 and NS-40:8) builds the same fixture through `PreviewModel` with the panel models set to the same values
   and through the CLI, and compares every new name. The Remove table has a name with accented letters
   (`caf` + e-acute + `.txt` with `--remove-accents`) and a name that starts with a surrogate pair (an emoji, then `a.txt`,
   with `--remove-first 1`: the pair goes whole, as the rule does). Write non-ASCII text as `\u` escapes in the tests.
9. Given rules from several panels in one line (`--replace x y --name-reverse --ext-upper --remove-digits --prefix p_`), then
   the names are the same in any order of the options (a test shuffles three orders); every option of this story counts as
   "a rule option given" for NS-38's "needs at least one rule" check.
10. Given the help (NS-43), then every new option word (26: `--name-fixed`, `--name-empty`, `--name-reverse`, `--ext-lower`,
    `--ext-upper`, `--ext-fixed`, `--ext-extra`, `--ext-remove`, and the eighteen `--remove-...` words of criterion 5) is
    listed in `HelpApply` on its own line, after NS-40's lines and before `--dry-run`, each starting with the option word and
    its value (`<text>`, `<n>`), one short sentence, at most 79 ASCII columns, aligned at NS-40's sentence column (the
    longest word here, `--remove-double-spaces`, fits it; no line is re-aligned), a continuation line indented to the
    sentence's column. NS-43's `EveryWord_IsInTheHelp`, `HelpLines_AtMost79_AsciiOnly` and `HelpTexts_AreInTheComposedHelps`
    pass, and `CommandLineParserTests.Words_AreTheDispatchedWords` is updated to the new list. The usage lines, `NoRule` and
    the last help line are not edited (NS-40:11). The apply help becomes long (about 70 lines): accepted, one line per
    setting, no group headers.
11. Given the new strings, then `CmdStringsTests.EveryKeyResolves` passes and each new key is used by a test that checks its
    exact text: `OneModeOnly` (twice), `NeedsOption`, `BlankText`, `BlankExtension`, `EmptyOptionText`, `RemoveRangeOptions`,
    `SkipSentenceMarker`.

## Out of scope
- RegEx, Move/Copy, Auto date, Folder name (NS-76); saved presets (NS-69); recursion and filters (NS-41).
- Short forms of the options (`-n`); a `--name-text` option shared by modes (R1 makes it needless).

## Decisions
- One flag per mode, or `--name <mode>` plus a text flag? **Decision:** one flag per mode (R1): the text belongs to the mode
  that uses it, so no text is ever ignored and no combination needs a rule of its own; the cost is a few more words in the
  help (po, 2026-10-24).
- `--name-empty` or `--name-remove`? **Decision:** `--name-empty`: `--remove-...` is another rule, and a script reader should
  not have to guess which one empties the name (po, 2026-10-24).
- A modifier with no option, or a value for a field the mode turns off? **Decision:** the first is an error (R2), the second is
  ignored (R3). A script that says `--remove-whole-word` alone would otherwise do nothing and still count as "a rule was
  given"; a script that says `--number-pad` in letters mode has a rule that works, and the window ignores it too (po,
  2026-10-24).
- Blank text? **Decision:** refused (R4) when the rule would skip it: `NameRule` skips white space, `ExtensionRule` skips dots
  and spaces, the text rules skip only empty text, so a single space is valid there (the message differs by what the rule
  skips) (po, 2026-10-24).
- The window's "Until then ... is skipped." sentence? **Decision:** cut by one helper at the marker, replacing the
  numbering-only sentence of NS-40, because the command line fails instead of skipping; one helper keeps NS-76 from adding a
  string per panel (po, 2026-10-24).
- Does the Remove panel get a flag per setting? **Decision:** yes, eighteen: it is what the window offers, and a script that
  cleans names needs the classes (digits, accents, double spaces) most (po, 2026-10-24).

## Log
- 2026-10-24: created (po); Draft until NS-40 is merged, so the option pattern is settled by the first PR.
- 2026-10-24: NS-40 approved (review of its code): criteria written against its parser, `ApplyPanels` and help; the split of
  `ParseApply` is the chore NS-79, which this story follows; set Ready, to start after NS-40, NS-69 and NS-79 are merged (po).
- 2026-10-24: PR #75 review applied: the two tests naming the old key are edited, blank counts refused, `README.md` example (po).
- 2026-10-24: In Progress (builder).
- 2026-10-24: story text corrected by the builder: the old-key lines in `CommandLineParserTests` are 623-624 (not 550-551); the Words and help-order tests also change (`ApplyHelp_ListsPresetOptions` now expects `--remove-double-spaces` before `--preset`).
- 2026-10-24: In Review (builder), PR #78.
- 2026-10-24: Done (tech-lead), PR #78 approved and squash-merged; review nits 1-3 folded (non-ASCII parity names as
  `\u` escapes, a space in the word list, blank lines in a test file). Follow-up (nit 4, style): `NameReader.ReadOwn`
  mixes `string.Equals` with `==` and a nested ternary for the mode, and `ExtensionReader` ignores the return of
  `TryChoice`: one shared style (a small mode table) would read better; `RemoveReader.ReadOwn` (about 45 lines, several
  `value!`) could split into `ReadCount` and `ReadText`.
