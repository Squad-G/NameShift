# NS-46: Clear row messages

Status: In Progress
Depends on: NS-52
Area: app

## Why
Understand why a row has a problem and what to change. Today the Status text names the problem (`Reserved by Windows:
CON`); each message should also say what to do. (Column sorting is NS-66.)

## Acceptance criteria
Changes: `ConflictText` in `NameShift.Ui.Preview`, `Strings.resx` and the doc comments in `Strings.cs` that quote the old
texts; in Core, `InvalidNameCheck.MaxNameLength` and `PathLengthCheck.MaxPathLength` (both `private const` today) become
`public const int`, with the same values (255 and 259) and no other change to the checks; and the existing tests that quote
the old words (`ConflictTextTests.cs`, and any other test that asserts a conflict text, found by searching the tests for
the old sentences). Conflict codes, the report, the checks' behaviour and the summary (`1 conflict`, `N conflicts`) are
unchanged. The detail rules are unchanged: the characters and the name in the text are escaped as `U+XXXX` for control
characters (NS-52:6) and the messages are joined with `; ` (`Strings.ConflictSeparator`).
1. Given each conflict, then `ConflictText.Message` returns exactly:
   - InvalidCharacter, detail `?`: `Windows does not allow this character in a name: ?`
   - InvalidCharacter, detail `?*` (two or more characters): `Windows does not allow these characters in a name: ? *`
   - InvalidCharacter, no detail: `Windows does not allow these characters in a name: ` (ends after the colon and space)
   - ReservedName, detail `CON`: `Windows reserves the name CON: change it`
   - ReservedName, no detail: `Windows reserves this name: change it`
   - TrailingDotOrSpace: `A name cannot end with a dot or a space: remove it`
   - EmptyName: `The new name is empty: keep some text in the name`
   - NameTooLong: `The name is longer than 255 characters: shorten it`
   - DuplicateTarget: `Another item gets the same new name: change one of them`
   - TargetExists: `An item with this name already exists in the folder: choose another name` ("item": it can be a folder)
   - PathTooLong: `The full path would be longer than 259 characters: shorten the name`
2. Given a control character in the detail (a tab, `U+0001`), then it is still shown escaped, never raw (the NS-52
   tests keep their meaning with the new wording); a one-character detail that is a control character uses the
   "this character" text with the escape (`...: U+0009`).
3. Given an InvalidCharacter or ReservedName with no detail, then the text is the no-detail text of criterion 1 and nothing
   throws (the old test of the empty detail is updated to these two texts).
4. Given two conflicts on one row (`?` and a duplicate), then the Status text is the two sentences joined by `; `.
5. Given the numbers 255 and 259 in the messages, then `ConflictText` reads them from `InvalidNameCheck.MaxNameLength` and
   `PathLengthCheck.MaxPathLength`, so the text cannot disagree with the checks. Tests: the NameTooLong message contains
   the value of `InvalidNameCheck.MaxNameLength` (255) and the PathTooLong message the value of
   `PathLengthCheck.MaxPathLength` (259); `NameTooLong` is flagged for a name of 256 characters and not for 255.
6. Given the rule-error cells of the New Name column (`Error: ...`, NS-13), then they are unchanged.

In the window (by hand, listed in the PR):
7. The Status column's default width (now 240 logical pixels) is raised so the longest single sentence
   (`An item with this name already exists in the folder: choose another name`) fits at 100 % scale; a cell with two
   sentences may be cut, and widening the column shows the rest; a screen reader reads the whole cell.

## Out of scope
- Column sorting (NS-66).
- Fixing the problem for the user (no "auto-resolve" button).
- Translating the text (the strings stay in `Strings.resx`).

## Decisions
- Better than classic renamers that show only a red cell. **Decision:** every flag has a sentence (po, 2026-10-18).
- Wording? **Decision:** a short statement, a colon, then the action in the imperative, with the limits quoted, so the
  user reads cause and fix in one line; the texts above are final (po, 2026-10-24).
- Split? **Decision:** column sorting moves to NS-66: it changes the list order, the selection and the numbering order,
  which is a different concern from the wording (po, 2026-10-24).
- Limit numbers in the text? **Decision:** quoted, since "too long" without the limit sends users to look it up; the
  numbers are read from the checks' constants (po, 2026-10-24).

## Log
- 2026-10-18: created (po).
- 2026-10-24: sorting split out to NS-66; exact wording written and criteria made testable against `ConflictText`; set Ready (po).
- 2026-10-24: In Progress (builder).
