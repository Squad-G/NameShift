# NS-83: Collapsible rule panels, and a bottom row that fits

Status: Ready
Depends on: NS-11, NS-15
Area: app

## Why
The rule panels take much of the window. A panel the user does not use can shrink to its title line, which gives the file
list more room, and the tallest open panel (Remove) is no longer cut off at the bottom (David, testing, 2026-10-24).

## What the code does today (checked 2026-10-24)
`MainForm` puts the `RulePanelStack` in `PanelHost`, inside the bottom panel of `_rightSplit` (`FixedPanel.Panel2`, a
fixed 240 logical pixels at start). The stack is a `FlowLayoutPanel` with `WrapContents = true` and `AutoScroll`, so the
panels wrap into rows, and the Remove panel (four groups, over 400 px) is taller than the 240 px area: its lower rows
are clipped or need a scroll. The cut-off is a symptom of the fixed height, so it is fixed here, not in a separate story.

## Acceptance criteria
Logic in `NameShift.Ui` with tests on net10.0 and net48; the App keeps thin controls. Names are indicative. Persistence
is NS-84 (this story keeps the state in memory only).

Model (`NameShift.Ui`, a `PanelCollapseModel` over the `RulePanelHostModel`; the key of a panel is its `Stage`):
1. Given a start with no saved state, then a panel starts collapsed when its check box is off and open when it is on
   (after any preset applied at start, NS-37, so the rules that come back are visible). A constructor takes an optional
   `IReadOnlyDictionary<RuleStage,bool>` of saved states (used by NS-84): a panel in it takes that value, any other
   panel the default above. Tests: all off gives all collapsed; one on gives that one open; saved overrides; a saved
   entry for an unknown stage is ignored.
2. `Toggle(panel)` and `SetCollapsed(panel, value)` change one panel and raise `Changed` once, and only when the value
   changes. `IsCollapsed(panel)` reads it. Tests: toggle twice returns to the start; setting the same value raises
   nothing; an unknown panel throws `ArgumentException`.
3. Given a collapsed panel whose check box goes from off to on (by a click, a key, the title's Alt mnemonic, or a preset
   or remembered rules loading), then it opens. Given a check box going from on to off, then the panel stays as it is
   (it does not collapse by itself). Reset leaves the collapse state alone. Opening a panel by hand does not tick it.
   Tests: each transition, with `RulePanelHostModel` and a fake panel model.
4. Given the size of each panel (its width, its open height, its title-only height, and whether it is collapsed), the
   margin between panels and the available width, then a pure function (for example
   `PanelFlow.Measure(panels, margin, availableWidth, minimum, cap)`) lays the panels out in pipeline order the way the
   wrapping `FlowLayoutPanel` does (left to right; a panel that does not fit in the remaining width starts a new row; a
   panel wider than the width sits alone on its row) and returns the rows, the total height and whether a vertical
   scroll is needed. A panel's height is its title-only height when collapsed, its open height otherwise; a row is as
   tall as its tallest panel; the total is the sum of the row heights plus margins. The result is never below `minimum`
   and never above `cap` (the window area minus the file list's minimum, 160 logical pixels); above the cap the height
   is the cap and the answer is "scroll". Tests: all collapsed (one or a few short rows); one open; two open in one row
   (the tallest wins); two open in separate rows (heights add); collapsing the tallest shrinks its row to the next one;
   a narrower width wraps more rows and makes the total taller; a panel wider than the width; the cap and the scroll
   flag; the minimum; zero panels.
   The width passed in is the usable width: the host's client width, minus the vertical scroll bar's width when one
   is shown. `Measure` is run in two steps to settle the scroll bar and wrap loop: first with the full client width; if
   the answer is "scroll", again with the client width minus the scroll bar width, and that second answer is final
   (more narrowing can only add rows, so the cap is still hit and the scroll stays on; it cannot flip back). Tests: a
   width where the first pass fits without scrolling; one where the first pass scrolls and the narrower second pass
   wraps one more row; the boundary width (exactly the width of the panels) with and without the scroll bar width.
   A panel wider than the usable width also makes the stack scroll horizontally (it is `AutoScroll`): kept as today, and
   the horizontal bar's height is added to the total by the App (by hand).
5. Given the keyboard, then each title has a focusable toggle (see 8) reached by Tab after the Reset button; Space and
   Enter toggle it (a `Button`'s normal behaviour). A pure Ui function gives its accessible name and the arrow: collapsed
   `▸` (U+25B8) with the name `Expand <title>`, open `▾` (U+25BE) with `Collapse <title>`, where `<title>` is
   `RulePanelText.PlainTitle` (no `&`). The glyphs and the two name formats live in `Strings.resx` only (the Ui function
   formats them). A collapsed panel's content is not in the Tab order. Tests: the arrow and the
   name for both states.

Window (App; by hand, written in the PR, with a screenshot of each state):
6. The panels keep the current layout: a wrapping `FlowLayoutPanel` (`WrapContents = true`) in pipeline order with a
   vertical scroll bar when needed. It is not changed to a single row. Each frame keeps the width it has when open, also
   when collapsed. Rule (frames are `AutoSize`, so they would shrink when the content is hidden): before the content is
   hidden the frame measures its open width and sets `MinimumSize.Width` to it; that width is measured again after a
   DPI or font change while open, and kept (not re-measured) while collapsed. Expanding clears nothing, the content
   returns at that width. Checked by hand: toggle every panel and the panels to its right do not move sideways.
7. A collapsed panel shows only its title line: the enable check box with the number and name, the Reset button (enabled
   by `CanReset` as today), and the arrow. The content is hidden. The active look (accent border, bold title) is kept
   when collapsed, so a collapsed panel that does something is still marked. A collapsed panel with its check box
   ticked still takes part in the rename; collapsing never changes the rules.
8. The arrow is a flat `Button` at the right end of the title line, after Reset in the Tab order, with the accessible
   name of 5, drawn with the window's UI font (the arrows fall back to a symbol font if the UI font lacks them: by hand,
   a screenshot at 100 %, 125 % and 150 %, both glyphs readable), and no Alt key (the top-level Alt letters stay as they are: `WindowMnemonicTests` must still pass without a
   new letter). The title's check box keeps its mnemonic. A click on the arrow toggles; a click on the check box only
   ticks it (and opens it, 3).
9. The bottom area's height (the `_rightSplit` bottom panel) is set from criterion 4 each time a panel opens or
   collapses, when the window is resized (the available width changes), and at start, so the wrapped panels always fit:
   Remove open shows all its rows with no clipping at 100 %, 125 % and 150 % scaling. Collapsing panels shortens their
   rows, and the file list takes the freed room. When the content is taller than the cap, the panel area scrolls
   vertically instead of clipping (the file list keeps its 160 px minimum). The splitter between the list and the bottom
   area is no longer dragged by hand (`IsSplitterFixed`), since the height now follows the content. The action buttons
   (`ButtonsHost`) still fit in the bottom area when all panels are collapsed (the minimum of criterion 4 is their
   preferred height). `_rightSplit.Panel2MinSize` (120 logical pixels in `MainForm.OnLoad` today) is removed, so it does
   not hold the area open when everything is collapsed; `Panel1MinSize` (160) stays and gives the cap. The splitter
   distance is set in code only (with `IsSplitterFixed`).
10. Guard: whatever collapses a panel (the arrow, or code later), focus is never left in a hidden control: if focus is
    inside the content, it moves to the arrow (by hand, with a keyboard-only pass).

## Out of scope
- Saving the state (NS-84); collapse in presets (see decisions); a "collapse all" or "expand all" command; animation;
  drag to reorder; the command line.

## Decisions
- Do presets carry the collapse state? **Decision:** no: it is window layout, not a rule setting. The preset format and
  `Preset.FromPanels` do not change, and loading a preset changes the collapse only through criterion 3 (a panel that
  becomes enabled opens), so a rule that works is never hidden (po, 2026-10-24).
- Collapsed width? **Decision:** unchanged (David): the row does not jump when a panel toggles (2026-10-24).
- Single row or wrapping? **Decision (lead, 2026-10-24):** keep the wrapping `FlowLayoutPanel` with a vertical scroll
  bar, as David's screenshot shows; "one scrolling row" was a wording slip in the question to David, not his request.
  A collapsed panel is title-height only.
- Bottom area height? **Decision:** the total height of the wrapped rows (each row as tall as its tallest open panel),
  within a minimum and a cap that leaves the list 160 px, then a vertical scroll rather than clipping: never hide a
  control without a way to reach it (po, 2026-10-24, reworked after the lead's decision).
- Splitter drag? **Decision:** off, since the height follows the panels and a manual height would fight the rule;
  simple over configurable. This replaces `rightSplit` in NS-37, which is dropped from it (po, 2026-10-24).
- Clipped Remove panel: separate bug story? **Decision:** part of this one: the same height rule fixes it, and
  building the row twice would waste a round (po, 2026-10-24).
- Unticking collapses? **Decision:** no: a panel the user is editing must not vanish under the pointer (po, 2026-10-24).
- Arrow shortcut? **Decision:** none beyond Tab then Space or Enter: it avoids using up an Alt letter (po, 2026-10-24).
- Why a pure layout function? **Decision:** the height depends on how panels wrap at the current width, which is easy to
  get wrong in the window and cheap to test as plain numbers (po, 2026-10-24).
- Why a model for something this small? **Decision:** CLAUDE.md puts window logic in `NameShift.Ui` with tests; the
  default, the tick-opens rule and the height function are exactly what a UI bug hides in (po, 2026-10-24).

## Log
- 2026-10-24: created from David's decisions (collapsed keeps width and shows check box, name, Reset, arrow; row height
  follows the tallest open panel; tick opens; first run: unticked panels collapsed). Code checked: the stack wraps with
  a fixed 240 px bottom area, so the clipped Remove panel is part of this story. Set Ready, with persistence in NS-84 (po).
- 2026-10-24: lead correction: keep the wrapping layout with vertical scroll (no single row); the bottom height is the
  wrapped rows' total, each row as tall as its tallest open panel, capped. Criteria 4, 6 and 9 reworked (po).
- 2026-10-24: PR #82 review applied: usable width and the scroll bar loop, `Panel2MinSize` removed, how a collapsed frame
  keeps its width, arrow glyphs in `Strings.resx` and the UI font, NS-15 dependency, criterion 10 kept as a guard (po).
