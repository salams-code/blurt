# 19 — Visual polish across overlay, settings, and onboarding

Status: ready-for-human
Type: HITL

## Parent

[.scratch/blurt/PRD.md](../PRD.md)

## What to build

A visual pass over the WPF surface — the status overlay pill, the settings
window, and the onboarding wizard — for a cleaner, more modern look. Establish a
small, consistent visual style (spacing, typography, colours, rounded surfaces,
subtle states) and apply it across all three. WPF supports full custom styling,
so this is purely a design/appearance effort; behaviour is unchanged.

Best done **after** the provider (17) and model (18) UI changes land, so the new
controls are styled once rather than restyled.

## Acceptance criteria

- [ ] The overlay pill, settings window, and onboarding wizard share a consistent, modern visual style.
- [ ] No behavioural regressions — all existing flows still work and the test suite stays green.
- [ ] The look is reviewed and approved by the user (HITL).

## Blocked by

- 17 — Switchable refinement provider with persistent key
- 18 — Use the selected Whisper model + per-selection download guidance
