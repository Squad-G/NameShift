# NS-54: Visual polish

Status: Draft
Depends on: NS-15, NS-55, NS-30
Area: app

## Why
The main window is plain default WinForms and David finds it "a bit ugly". NameShift should look clean and modern
while staying tiny: no third-party UI library and no theme engine that bloats the installer. Styling is done once, on
the finished layout (preview, conflicts, selection, buttons).

## Acceptance criteria
1. Given the main window, then the spacing between controls, the panel margins and the alignment of the tree, file
   list, rule panels and buttons come from one spacing class of named constants (for example 4, 8, 12 px at 100 %
   scale). This story migrates the existing panels' literal spacing values (NS-12, NS-53, the panel frame, the file
   list) to it. A test checks that each panel uses the constants (reads the App sources for literal `Padding(` and
   `Margin` numbers outside the spacing class).
2. Given the main window and every dialog, then the font is the system message-box font (Segoe UI on Windows 10/11) at
   the system size, and it follows the Windows text-size setting. A test checks the controls use
   `SystemFonts.MessageBoxFont`.
3. Given any theme, then every colour comes from `SystemColors` or from one accent colour derived from
   `SystemColors.Highlight`, never from a hard-coded RGB value. Given a Windows high-contrast theme, the accent helper
   returns `SystemColors.Highlight` itself (when `SystemInformation.HighContrast` is true) and the window stays fully
   readable. A test checks that no hard-coded colour is used outside the one derivation helper, and the helper's
   high-contrast result.
4. Given a rule panel, then it has a subtle border and its title is bold or semibold at the same size as the body text,
   not clipped at 200 %. NS-11's active-panel highlight (border, bold title, screen-reader description) is kept
   unchanged, with the new subtle border only on inactive panels, so the active state is still not colour-only.
5. Given the main buttons (Rename, Revert, Reset) and the panel titles, then each has a small icon that is sharp at
   100 %, 125 %, 150 % and 200 % scale (drawn in code, or our own embedded resources, one image per scale). The icons
   are original, drawn for NameShift, never copied from another product or an icon set. Button text stays, so the icon
   is never the only label.
6. Given the same folder and rules as before the story, then the preview, conflicts, selection and rename results are
   identical: no behaviour changes, and all existing tests pass unchanged.
7. Given the app's Release output folder (total size of the published files), then its size after the story is at most
   0.5 MB more than before. The installer's own size is NS-44's criterion.
8. Given the finished window, then David walks through this checklist by eye at 100 % and 200 % scale, in light mode
   and in a high-contrast theme: aligned edges, even gaps, one font, one accent colour, readable titles, icons
   crisp, nothing clipped. This is David's manual check, tracked in the PR.

## Out of scope
- Custom skins or themes.
- Animations.
- Dark mode (see Decisions).
- Third-party UI libraries.

## Decisions
- Why this story? **Decision:** David looked at the window on 2026-10-18 and said it is "a bit ugly ... will see at the
  end"; it is built at the end of the first usable slice, after NS-15 (po, 2026-10-18).
- Build it now or after more panels? **Decision:** later: it restyles every panel and migrates their spacing, so it
  goes after the Name/Extension and Remove panels (NS-55, NS-30) to avoid redoing it and merge conflicts; it stays
  Draft until then and criteria 1 and 5 are re-checked against the panels that exist (po, 2026-10-23).
- Dark mode in this story or later? **Decision:** a separate later story, not written yet. It needs its own colour
  work, owner-drawn parts of WinForms and its own tests, and would make this story large; this story first makes all
  colours go through system colours, which is what dark mode will build on (po, 2026-10-18).
- One accent colour from where? **Decision:** derived from the system highlight colour, so it respects the user's
  Windows accent and high-contrast themes (po, 2026-10-18).
- Icons: **Decision:** drawn in code or our own embedded resources, original, no icon font or package, to keep the binary small (po, 2026-10-18).

## Log
- 2026-10-18: created (po).
- 2026-10-18: criteria 1-5 and 7 tightened: size measured on the Release output folder (no installer yet), spacing, font, colour, title and icon checks made testable (po, from the PR #19 review).
- 2026-10-23: reordered after NS-30; kept Draft on purpose (po).
