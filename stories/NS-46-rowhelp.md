# NS-46: Clear row messages

Status: Ready
Depends on: NS-52
Area: app

## Why
Understand why a row has a problem and what to change. Today the Status text names the problem (`Reserved by Windows:
CON`); each message should also say what to do. (Column sorting is NS-66.)

## Acceptance criteria
Only `ConflictText` in `NameShift.Ui.Preview` and `Strings.resx` change, plus the existing tests that quote the old words
(`ConflictTextTests.cs`, and any other test that asserts a conflict text, found by searching the tests for the old
sentences). Conflict codes, the report, the checks and the summary (`1 conflict`, `N conflicts`) are unchanged. The
detail rules are unchanged: the characters and the name in `{0}` are escaped as `U+XXXX` for control characters (NS-52:6)
and the messages are joined with `; ` (`Strings.ConflictSeparator`).
1. Given each conflict, then `ConflictText.Message` returns exactly:
   - InvalidCharacter, detail `?*`: `Windows does not allow these characters in a name: ? *`
   - ReservedName, detail `CON`: `Windows reserves the name CON: change it`
   - TrailingDotOrSpace: `A name cannot end with a dot or a space: remove it`
   - EmptyName: `The new name is empty: keep some text in the name`
   - NameTooLong: `The name is longer than 255 characters: shorten it`
   - DuplicateTarget: `Another file gets the same new name: change one of them`
   - TargetExists: `A file with this name already exists in the folder: choose another name`
   - PathTooLong: `The full path would be longer than 259 characters: shorten the name`
2. Given a control character in the detail (a tab, `U+0001`), then it is still shown escaped, never raw (the NS-52
   tests keep their meaning with the new wording).
3. Given an InvalidCharacter with no detail, then the message ends after the colon and space as before and does not throw.
4. Given two conflicts on one row (`?` and a duplicate), then the Status text is the two sentences joined by `; `.
5. Given the 255 and 259 in the messages, then they come from the same constants the checks use (`InvalidNameCheck` and
   `PathLengthCheck` expose them as `public const int`, or `ConflictText` is given them), so the text cannot disagree with
   the check; a test asserts that `NameTooLong` is flagged for a name of 256 characters and not for 255, and that the
   message contains `255`.
6. Given the rule-error cells of the New Name column (`Error: ...`, NS-13), then they are unchanged.

In the window (by hand, listed in the PR):
7. The Status column's default width (now 240 logical pixels) is raised so the longest single sentence
   (`A file with this name already exists in the folder: choose another name`) fits at 100 % scale; a cell with two
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
