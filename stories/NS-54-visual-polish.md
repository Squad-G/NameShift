# NS-54: Visual polish

Status: Ready
Depends on: NS-15, NS-55, NS-30
Area: app

## Why
The main window is plain default WinForms and David finds it "a bit ugly". NameShift should look clean and modern while
staying tiny: Windows system colours, one accent, even spacing and a few original icons, with no third-party UI library and
no theme engine. Styling is done once, on the finished layout.

## Acceptance criteria
Look decided by David (2026-10-24): Windows system colours plus one accent from the Windows accent colour, spacing of 4, 8
and 12 px, original icons drawn in code. Logic that can be tested goes in `NameShift.Ui`; the App only draws.

Spacing:
1. `NameShift.Ui` gets `Spacing` with `public const int Small = 4, Medium = 8, Large = 12` (96-DPI logical pixels, scaled
   with `LogicalToDeviceUnits` as today). Every `Padding(` and `Margin` in `src/NameShift.App` takes its numbers from
   `Spacing` (0 is allowed as a literal). A test in `NameShift.Ui.Tests` reads the `.cs` files of `src/NameShift.App`
   (found by walking up from the test folder to the folder holding `NameShift.sln`) and fails on any digit other than 0
   inside a `Padding(` or `Margin` expression, naming the file and line.
2. Given the migration, then panel and control sizes (widths, heights, column widths) are not part of this: only gaps and
   padding. The window's layout is otherwise unchanged.

Font:
3. Given the main window and every dialog, then the font is `SystemFonts.MessageBoxFont` (Segoe UI at the system size),
   set once on `MainForm` and inherited. The same test scan fails on a font family name literal (`"Segoe UI"`, `"Arial"`,
   `"Microsoft Sans Serif"`, ...) or `new Font("` anywhere in the App sources.

Colours:
4. Given any colour in the App, then it comes from `SystemColors`, from the accent (criterion 5), or from the two named
   status colours in one `Palette` class (the green and the red of NS-13/NS-52 with their 4.5:1 contrast and high-contrast
   fallback, unchanged). The same test scan fails on `Color.FromArgb`, `Color.Red`-style named colours or `ColorTranslator`
   outside `Palette.cs`.
5. `NameShift.Ui` gets `AccentColor` with pure integer logic (colours as ARGB `int`, no System.Drawing):
   - `Derive(int windowsAccentArgb, int highlightArgb, bool highContrast)` returns `highlightArgb` when `highContrast` is
     true or the accent is 0 (unreadable); else the accent made darker, in steps, until its contrast ratio against white
     (`#FFFFFF`) is at least 4.5:1, keeping its hue; an accent that already passes is returned unchanged (alpha forced to
     `0xFF`).
   - `ContrastRatio(int a, int b)` is the WCAG relative-luminance ratio. Tests: black/white is 21.0 (within 0.01),
     identical colours 1.0; accent `#FFD700` (yellow) comes back with ratio >= 4.5; `#003399` (already above 4.5:1) is
     returned unchanged with alpha `0xFF`; high contrast returns the highlight; accent 0 returns the highlight.
   - In the App a small `AccentSource` reads the accent from the current user's `DWM\ColorizationColor` registry value
     (read only, no write, nothing sent anywhere) and falls back to `SystemColors.Highlight` when it is missing or unreadable.
6. The accent is used in exactly three places: the border and title colour of the active rule panel (NS-11), the Rename
   button's icon, and the focus/hover edge of the three action buttons if a custom one is drawn. Never as the only cue:
   the active panel keeps its bold title and its screen-reader description (NS-11).

Panels:
7. Given a rule panel, then inactive panels have a subtle border (`SystemColors.ControlDark`) and the title is bold at the
   body size, not clipped at 200 %. The active panel's border and bold title are kept (NS-11), now in the accent. By hand.

Icons:
8. Given the buttons Rename, Revert and Reset, then each has a small original icon left of its text, drawn in code with
   GDI+ at the device size (so sharp at 100, 125, 150 and 200 %), never copied from another product or an icon set. The
   text stays, so the icon is never the only label. Panel titles get no icon (decision below). In a high-contrast theme the
   icons are drawn in the button's text colour.
9. Given a button icon, then it is created once per DPI and disposed with the control (no handle leak): checked in review.

No change in behaviour:
10. Given the same folder and rules as before, then the preview, conflicts, selection, rename and revert results are
    identical: all existing tests pass unchanged.
11. Given the Release output folder of the App (the published files, in bytes), then its size after the story is at most
    0.5 MB larger; the PR states both numbers.
12. By hand, at 100 % and 200 % scale, in the light theme and in a high-contrast theme (checklist in the PR; David's
    check): aligned edges, even gaps, one font, one accent, readable titles, crisp icons, nothing clipped.

## Out of scope
- Custom skins or themes, animations, dark mode (its own later story), third-party UI libraries.
- Icons on panel titles, a new application icon, a logo (branding is David's).
- Changing layout, sizes or behaviour.

## Decisions
- Look? **Decided by David (2026-10-24):** the default proposal: Windows system colours plus one accent taken from the
  Windows accent colour, spacing 4/8/12 px, original icons drawn in code; nothing he disliked was noted.
- Why this story? **Decision:** David looked at the window on 2026-10-18 and said it is "a bit ugly"; built after the
  panels exist (po, 2026-10-18).
- Dark mode? **Decision:** a separate later story; this story routes colours through system colours, which dark mode builds
  on (po, 2026-10-18).
- Accent source? **Decision:** the DWM accent from the registry (read only) with the system highlight as fallback, because
  `SystemColors.Highlight` is not the user's accent colour on Windows 10/11; darkened until it reads on white at 4.5:1 so
  text in the accent is always legible (po, 2026-10-24).
- Panel title icons? **Decision:** none: eleven more icons cost size and design time and the bold title already marks the
  panel (po, 2026-10-24).
- Keep the green and red status colours? **Decision:** yes, in one `Palette` class; they carry meaning (changed, error) and
  were chosen for contrast (po, 2026-10-24).
- Tests that read source files? **Decision:** accepted for the three rules of this story (spacing, fonts, colours), so
  the style cannot drift; they are skipped with a clear message when the sources are not found (po, 2026-10-24).

## Log
- 2026-10-18: created (po).
- 2026-10-18: criteria 1-5 and 7 tightened: size measured on the Release output folder (no installer yet), spacing, font, colour, title and icon checks made testable (po, from the PR #19 review).
- 2026-10-23: reordered after NS-30; kept Draft on purpose (po).
- 2026-10-24: the panels it waited for are Done (all eleven). Kept Draft until David chose the look.
- 2026-10-24: David chose the default look; criteria rewritten to be testable (Ui `Spacing`, `AccentColor`, source scans) and the rest by hand; set Ready (po).
