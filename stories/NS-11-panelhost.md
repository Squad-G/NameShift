# NS-11: Rule panel host: enable and reset per rule

Status: Done
Depends on: NS-10, NS-1
Area: app

## Why
The bottom area where each rule is a panel I can switch on and reset.

## Acceptance criteria
1. Given the window, then the bottom area shows one panel per implemented rule in pipeline order, each with a number, an enable checkbox and a reset button.
2. Given a panel has any non-default value and is enabled, then its title or border is highlighted so I see at a glance which rules are active; resetting it removes the highlight.
3. Given a panel is unchecked, then its controls are disabled and it has no effect on the preview.
4. Given I press a panel's reset button, then only that panel's settings return to their defaults and it is unchecked; other panels are untouched.
5. Given any panel setting changes, then the rules list is rebuilt and the preview (NS-13) is notified at once, with no delay of its own: the preview alone debounces (150 ms), so delays never stack.
6. Given more panels than fit, then the area scrolls; panels keep a minimum readable size (checked by hand).
7. A panel is a reusable control with an interface (name, enabled, build rule, reset), so later rules add a panel without touching the host.

## Out of scope
- The specific panels (NS-12).

## Decisions
- Tabs or all visible? **Decision:** a scrolling stack of panels, all visible, because the pipeline is the point of the UI and tabs hide what is active (po, 2026-10-18).

- Panels start unchecked at launch (same as after Reset); remembering them is NS-37/NS-36. (tech-lead default, 2026-10-18; po may overrule)
- Until NS-12 merges the bottom area is empty; the frame's look is checked by hand with NS-12's panels. (tech-lead default, 2026-10-18; po may overrule)
- Panel contract in `NameShift.Ui.Rules` (`IRulePanelModel`, abstract `RulePanelModel`, `RulePanelHostModel`); the App only frames and stacks the content controls, composed in `RulePanels.Create()`. (tech-lead, 2026-10-18)
- The host rebuilds `Rules` and raises `RulesChanged` synchronously with no timer; the preview alone debounces. (tech-lead, 2026-10-18)
- Active = enabled and non-default: thicker `SystemColors.Highlight` border, bold title and an accessible description, never colour alone. (tech-lead, 2026-10-18)
## Log
- 2026-10-18: created (po).
- 2026-10-18: set Ready, first round of the minimal usable slice (po).
- 2026-10-18: In Progress (builder).
- 2026-10-18: In Review (builder): panel contract and host in NameShift.Ui, framed scrolling stack in the App; no panels yet (NS-12).
- 2026-10-18: Done (tech-lead): merged after VERDICT: APPROVE, review nits folded (focus moves to the check box when Reset disables, the title inherits the frame's font when inactive). David's manual checks M2–M5 (look, scrolling, minimum size, DPI, focus after Reset) come with NS-12's panels.
