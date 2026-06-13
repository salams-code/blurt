# 41 — Add screenshots to the README

Status: ready-for-human
Type: HITL (visual content + capture)

## Parent

[.scratch/blurt/PRD.md](../PRD.md)

## What to build

The README is text-only for now (by choice). Add screenshots so the app is
explained visually: the tray + Settings window, the first-run onboarding, and the
status overlay pill in action (listening / a mode flash). Place them inline in the
relevant README sections.

Best done once the UI is settled — in particular after the animated onboarding
(issue 40), so the onboarding shots match the final flow.

## Acceptance criteria

- [ ] Screenshots for: Settings, onboarding, and the overlay pill, embedded in the
      README at the right sections.
- [ ] **Identity-scrubbed** — no username, machine paths, or personal data visible
      (per the public-repo policy); use `%APPDATA%`-style placeholders if a path
      shows.
- [ ] Images committed under a sensible docs/asset path and referenced relatively.

## Blocked by

- None to start, but best sequenced **after** issue 40 (animated onboarding) so the
  onboarding screenshots reflect the final UI.

## Notes

Capture tips live in the UI-verification toolkit (screenshots go blank when the
display is off/locked — capture with the screen on).
