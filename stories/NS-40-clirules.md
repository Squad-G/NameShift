# NS-40: Command line: Add and Numbering options

Status: Draft
Depends on: NS-38, NS-12, NS-53, NS-61, NS-43
Area: app

## Why
Rename without the window: add text around names and number files from a script. First of the rule-option stories (the
original NS-40 asked for every rule at once; the rest is NS-75 and NS-76). Draft only until NS-43 is merged (it changes
the parser and adds the help test this story feeds); the criteria are final.

## Acceptance criteria
Code in `src/NameShift.Cmd` (`CommandLineParser`, `ApplyOptions`). The rules are built by the window's own panel models
(`AddPanelModel`, `NumberingPanelModel`), as `--replace` and `--case` already are, so defaults and meaning are the
window's by construction. Rules still run in the pipeline's fixed order whatever the typing order (NS-38:3).

Add (the Add panel):
1. Given `--prefix <text>` and `--suffix <text>`, then the text goes before and after the base name, the extension is
   left alone: `IMG_001.JPG` with `--prefix Trip_` gives `Trip_IMG_001.JPG`, with `--suffix _v2` gives `IMG_001_v2.JPG`.
2. Given `--insert <text> <position>`, then the text goes inside the base name at the 1-based position counted from the
   start, or from the end with `--from-end` (a flag that needs `--insert`). Given `--word-space`, then the Add panel's
   word-space option is on. A parity test per option (below) is the oracle for the exact names.
3. Given an empty text (`--prefix ""`), a position that is not a whole number from 1, `--from-end` without `--insert`,
   or `--insert` with an empty text, then exit 1 with one line saying which option is wrong (a new string each; the
   Add panel would silently skip the part, a script must hear about it), nothing renamed.

Numbering (the Numbering panel):
4. Given `--number`, then numbering is on with the panel's defaults (suffix, start 1, step 1, 0 digits, separator `_`):
   `a.txt`, `b.txt` give `a_1.txt`, `b_2.txt`. Any of the options below also turns numbering on, so `--number` alone is
   needed only for the defaults.
5. Given `--number-mode prefix|suffix|insert`, `--number-start <n>`, `--number-step <n>`, `--number-pad <n>` (digits),
   `--number-separator <text>`, `--number-position <n>` (insert mode), `--number-system decimal|hex|letters|roman`
   and `--number-case upper|lower`, then each sets the matching `NumberingPanelModel` property (`Mode`, `StartText`,
   `StepText`, `PadText`, `Separator`, `InsertPositionText`, `Numerals`, `UpperCase`). Examples:
   `--number-mode prefix --number-pad 3` gives `001_a.txt`, `002_b.txt`; `--number-system roman` gives `a_I.txt`,
   `b_II.txt`. Words are compared ignoring case; `hex` is the only spelling of Hexadecimal. `--number-start` is read in the chosen
   system, as the panel does (`--number-system roman --number-start V` starts at 5; with hex, `10` is 16); the CLI sets
   `Numerals` before `StartText`, because the `Numerals` setter rewrites the start. A test for roman and for hex.
6. Given a value the model refuses (`--number-start x`, `--number-pad 99`, `--number-system octal`, `--number-position 0`
   in insert mode, a start the chosen system cannot write), then exit 1 with the model's own error text
   (`StartError`, `StepError`, `PadError`, `InsertPositionError`) or, for an unknown word, the allowed values listed;
   nothing renamed. `--number-position` outside insert mode, `--number-pad` in letters or roman, and `--number-case` in
   decimal are accepted and ignored, as in the window (the panel disables those fields).
7. Given any option, then it takes one value after the flag (`--insert` two, `--from-end`, `--word-space` and `--number`
   none), may come in any position, may not repeat (the usual "can be given only once" error), and a value that is
   an option word counts as missing (NS-38's guard: `--prefix --dry-run` is an error, not a prefix `--dry-run`). Other
   values may start with a dash (`--number-separator -`).

Same as the window, and the rest:
8. Given each of criteria 1, 2, 4 and 5 (a table of cases covering every option), then a parity test (as NS-38:4) builds
   the same fixture through `PreviewModel` with `AddPanelModel` / `NumberingPanelModel` set to the same values and
   through the CLI, and compares every new name.
9. Given rules from several panels in one line (`--replace x y --prefix p_ --number --case upper`), then the names are
   the same in any order of the options (a test shuffles three orders).
10. Given the help (NS-43), then every new option is listed with one line in `apply --help` and in the main help (the
    NS-43 test that every parser option word appears in the help passes); the usage line (`Strings.Usage`) names them.
11. Given only a new option and no `--replace` or `--case`, then the "needs at least one rule option" check of NS-38 is
    satisfied by it.

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
