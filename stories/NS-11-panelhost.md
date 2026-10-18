# NS-11: Rule panel host: enable and reset per rule

Status: Ready
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

## Log
- 2026-10-18: created (po).
- 2026-10-18: set Ready, first round of the minimal usable slice (po).
