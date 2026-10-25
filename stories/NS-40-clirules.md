# NS-40: Command line: Add and Numbering options

Status: Done
Depends on: NS-38, NS-12, NS-53, NS-61, NS-43, NS-42, NS-39
Area: app

## Why
Rename without the window: add text around names and number files from a script. First of the rule-option stories (the
original NS-40 asked for every rule at once; the rest is NS-75 and NS-76). NS-42 and NS-39 are listed for the build
order only (all three change the parser, the runner and the help; NS-42 made the usage lines generic, so this story
does not edit them).

## Acceptance criteria
Code in `src/NameShift.Cmd` (`CommandLineParser`, `ApplyOptions`, `Strings.resx`). The rules are built by the window's
own panel models (`AddPanelModel`, `NumberingPanelModel`), as `--replace` and `--case` already are, so defaults and
meaning are the window's by construction. Rules still run in the pipeline's fixed order whatever the typing order
(NS-38:3). `ApplyOptions` must not grow one constructor parameter per option (NS-39 already added one, NS-75, NS-76 and
NS-69 follow): the tech-lead's brief picks a shape in which the parser fills the panel models it needs (an
`AddPanelModel` and a `NumberingPanelModel`, null when no option of that panel was given) and `BuildRules` reads them;
NS-69 later replaces a preset's panel entry with exactly these models.

Add (the Add panel):
1. Given `--prefix <text>` and `--suffix <text>`, then the text goes before and after the base name, the extension is
   left alone: `IMG_001.JPG` with `--prefix Trip_` gives `Trip_IMG_001.JPG`, with `--suffix _v2` gives `IMG_001_v2.JPG`.
2. Given `--insert <text> <position>`, then the text goes inside the base name at the 1-based position counted from the
   start, or from the end with `--from-end` (a flag that needs `--insert`). Given `--word-space`, then the Add panel's
   word-space option is on. A parity test per option (below) is the oracle for the exact names.
3. Given an empty text (`--prefix ""`), a position that is not a whole number from 1, `--from-end` without `--insert`,
   or `--insert` with an empty text, then exit 1 with one line saying which option is wrong (the Add panel would
   silently skip the part, a script must hear about it), nothing renamed. The usage errors have the NS-43:4 shape
   (message, `Strings.Usage`, `Strings.HelpPointer`) and these new strings: `EmptyPrefix` = `The text of --prefix cannot
   be empty.`, `EmptySuffix` = `The text of --suffix cannot be empty.`, `EmptyInsert` = `The text of --insert cannot be
   empty.`, `InsertNeedsValues` = `--insert needs two values: the text and the position.`, `BadInsertPosition` = `"{0}" is
   not a position of --insert: use a whole number from 1.`, `FromEndNeedsInsert` = `--from-end needs --insert.`. A
   missing value of any other new option is the generic `OptionNeedsValue` = `{0} needs a value.` (`{0}` is the option).

Numbering (the Numbering panel):
4. Given `--number`, then numbering is on with the panel's defaults (suffix, start 1, step 1, 0 digits, separator `_`):
   `a.txt`, `b.txt` give `a_1.txt`, `b_2.txt`. Any of the options below also turns numbering on, so `--number` alone is
   needed only for the defaults.
5. Given `--number-mode prefix|suffix|insert`, `--number-start <n>`, `--number-step <n>`, `--number-pad <n>` (digits),
   `--number-separator <text>`, `--number-position <n>` (insert mode), `--number-system decimal|hex|letters|roman` and
   `--number-case upper|lower`, then each sets the matching `NumberingPanelModel` property (`Mode`, `StartText`,
   `StepText`, `PadText`, `Separator`, `InsertPositionText`, `Numerals`, `UpperCase`). Examples:
   `--number-mode prefix --number-pad 3` gives `001_a.txt`, `002_b.txt`; `--number-system roman` gives `a_I.txt`,
   `b_II.txt`. Words are compared ignoring case; `hex` is the only spelling of Hexadecimal. `--number-start` is read in
   the chosen system, as the panel does (`--number-system roman --number-start V` starts at 5; with hex, `10` is 16);
   the CLI sets `Numerals` before `UpperCase` and `StartText`, because the `Numerals` setter rewrites the start and
   resets the case to the system's usual one. A test for roman and for hex, each with `--number-case lower` given before
   `--number-system` (the lower case is kept: `--number-case lower --number-system hex` never gives capitals).
6. Given a value the model refuses (`--number-start x`, `--number-pad 99`, `--number-system octal`,
   `--number-position 0` in insert mode, a start the chosen system cannot write), then exit 1 with the model's own error
   text (`StartError`, `StepError`, `PadError`, `InsertPositionError`: new string `OptionError` = `{0}: {1}`, the option
   and the model's text) or, for an unknown word, new string `BadChoice` = `"{0}" is not a value of {1}: use {2}.` (the
   value, the option, the allowed values as `prefix, suffix or insert`); nothing renamed. The model's error is read only
   for the fields that apply (`PadApplies`, `InsertPositionApplies`), as the window does. `--number-position` outside
   insert mode, `--number-pad` in letters or roman, and `--number-case` in decimal are accepted and ignored, as in the
   window (the panel disables those fields).
7. Given any option, then it takes one value after the flag (`--insert` two, `--from-end`, `--word-space` and `--number`
   none), may come in any position, may not repeat (the usual "can be given only once" error), and a value that is
   an option word counts as missing (NS-38's guard: `--prefix --dry-run` is an error, not a prefix `--dry-run`). Other
   values may start with a dash (`--number-separator -`).

Same as the window, and the rest:
8. Given each of criteria 1, 2, 4 and 5 (a table of cases covering every option), then a parity test (as NS-38:4) builds
   the same fixture through `PreviewModel` with `AddPanelModel` / `NumberingPanelModel` set to the same values and
   through the CLI, and compares every new name. The table has `--number-case lower` before `--number-system hex` and
   before `--number-system roman`.
9. Given rules from several panels in one line (`--replace x y --prefix p_ --number --case upper`), then the names are
   the same in any order of the options (a test shuffles three orders).
10. Given the help (NS-43), then every new option word (`--prefix`, `--suffix`, `--insert`, `--from-end`,
    `--word-space`, `--number`, `--number-mode`, `--number-start`, `--number-step`, `--number-pad`,
    `--number-separator`, `--number-position`, `--number-system`, `--number-case`) is added to
    `CommandLineParser.ApplyOptionWords` (so `Words` and the option-word guard see it), built from constants like the
    existing ones, and listed with its own line in `HelpApply` (the main help includes it), each line starting with the
    option word and its values, one short sentence, at most 79 ASCII columns (a continuation line is indented to the
    sentence's column; the column may move right to fit `--number-separator <text>`, which means every line of
    `HelpApply` is re-aligned). NS-43's `EveryWord_IsInTheHelp`, `HelpLines_AtMost79_AsciiOnly` and
    `HelpTexts_AreInTheComposedHelps` pass, and `CommandLineParserTests.Words_AreTheDispatchedWords` (which pins the
    exact list) is updated to the new list. The usage lines are generic (NS-42) and are not edited. The values of
    `--number-mode`, `--number-system` and `--number-case` are listed in their lines (`prefix|suffix|insert`,
    `decimal|hex|letters|roman`, `upper|lower`), as `--case` lists its four.
11. Given only a new option and no `--replace` or `--case`, then the "needs at least one rule" check of NS-38 is
    satisfied by it (the parser's check reads "any rule option given", not the two old ones). `Strings.NoRule` becomes
    `Give at least one rule option, such as --replace or --case.` (true for every later rule story, so none edits it
    again) and the last line of `HelpApply` becomes `Give at least one rule option. Options can come in any order.`

## Out of scope
- The other rules: Name, Extension, Remove (NS-75); RegEx, Move/Copy, Auto date, Folder name (NS-76).
- Saved settings (NS-69), the numbering restart per folder (needs recursion: NS-41), mask and filters (NS-41).

## Decisions
- One story for every rule? **Decision:** no: Add and Numbering first (the two most asked for in scripts), the rest in
  two follow-ups, so each PR stays reviewable and the parser change gets exercised on a small set first (po, 2026-10-24).
- Flags per rule or one `--rule add:...` syntax? **Decision:** plain flags, one per setting: they read well in a shell,
  need no extra escaping and are what NS-38 already does (po, 2026-10-24).
- Does `--number-*` need `--number`? **Decision:** no, any numbering option turns it on; fewer words, and a numbering
  option with numbering off would be an error nobody wants to see (po, 2026-10-24).
- Refuse the values the window would silently ignore? **Decision:** yes for text that is empty or a position that is
  unusable (the window shows it in red; a script has no red), but not for options the panel disables (po, 2026-10-24).
- A new rule story adds its options in its own PR? **Decision:** yes, from NS-75 and NS-76 on; the help test of NS-43
  makes a missing option fail (po, 2026-10-24).

## Log
- 2026-10-18: created (po).
- 2026-10-24: reduced to Add and Numbering with concrete options; rest split into NS-75 and NS-76; Draft until NS-43
  is merged (po).
- 2026-10-24: NS-43 merged. Re-checked against its code (word lists, guard, 79-column help tests, usage shape): error
  strings chosen, `ApplyOptions` shape constraint, `NoRule` and help text made generic, depends on NS-42 and NS-39 for the
  build order; set Ready, third of NS-42, NS-39, NS-40, NS-69 (po).
- 2026-10-24: In Progress (builder).
- 2026-10-24: In Review (builder), PR #74.
- 2026-10-25: Done, PR #74 squash-merged into `dev` after the reviewer's APPROVE on its re-review. Hand checks pending
  for David: `apply --help` in cmd, PowerShell and Windows Terminal (alignment, the two wrapped lines); on a scratch
  folder, one `--number --dry-run` and one real `--prefix` run, then `undo` (tech-lead).
- 2026-10-25: Note for NS-75: `CommandLineParser.ParseApply` is now about 440 lines; split it first (extract the Add and
  Numbering tails into their own methods) before adding the next panel's options (tech-lead).
- 2026-10-25: Note for the po: criterion 6 gives `--number-pad 99` as a refused value, but the window's pad maximum is
  255 (`NumberingRule.MaxPad`), so 99 is accepted, as in the window. The tests use `--number-pad 256`; the example in
  criterion 6 could read `256` (tech-lead).
