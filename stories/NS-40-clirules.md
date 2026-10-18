# NS-40: Command line: rules given as options

Status: Draft
Depends on: NS-38, NS-12
Area: app

## Why
Rename without a saved preset.

## Acceptance criteria
1. Given `--replace "a=b"`, `--case lower`, `--prefix x_` and similar flags for every implemented rule, then they build the same rule list as the matching panel would.
2. Given options and `--preset` together, then the options are added to the preset's rules and an option for a rule replaces that rule's preset values.
3. Given an unknown option or invalid value (`--case sideways`), then exit code 1 with the allowed values.
4. Given `--save-preset NAME` with rules, then they are saved as a preset in the shared format.
5. Rules always run in the fixed pipeline order whatever order the options are typed in.
6. A new rule story adds its option in the same PR (listed in the rule stories' review).

## Out of scope
- Anything not listed above.

## Decisions
- Escaping? **Decision:** values use the shell's own quoting; no extra escape syntax (po, 2026-10-18).

## Log
- 2026-10-18: created (po).
