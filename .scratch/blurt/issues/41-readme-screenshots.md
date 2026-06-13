# 41 — Add screenshots to the README

Status: done (2026-06-14 — path-safe screenshot set)
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

- [x] Screenshots for: Settings, onboarding, and the overlay pill, embedded in the
      README at the right sections.
- [x] **Identity-scrubbed** — no username, machine paths, or personal data visible
      (per the public-repo policy); use `%APPDATA%`-style placeholders if a path
      shows.
- [x] Images committed under a sensible docs/asset path and referenced relatively.

## Blocked by

- None to start, but best sequenced **after** issue 40 (animated onboarding) so the
  onboarding screenshots reflect the final UI.

## Notes

Capture tips live in the UI-verification toolkit (screenshots go blank when the
display is off/locked — capture with the screen on).

## Comments

**2026-06-14 (agent) — done (path-safe set; user chose path-safe shots).**

Three identity-verified screenshots under `docs/assets/`, referenced relatively:
- `settings.png` — the Settings window top (Privacy/Full cloud, mic, OpenAI
  refinement, model, **masked** API key). No `%APPDATA%` path in view; embedded
  under **## Configuration**.
- `onboarding-tutorial.png` + `onboarding-tutorial-flex.png` — two cards of the new
  animated tutorial (issue 40), showing the live status pill ("listening") and the
  Flex mode flash ("• Bullets", blue). They double as the **overlay-pill-in-action**
  shots. Embedded under **### First-run onboarding** (which also gained a line about
  the tutorial).

Captured from the re-published portable via `--settings` / `--tutorial` (+ UIA to
reach the Flex card) and `capture.ps1`; each was eyeballed for identity leaks before
committing. Deliberately **excluded** the onboarding *model* step and the lower
Settings rows, which surface the real `%APPDATA%\Blurt\…` path (Windows username).
A standalone `--overlay` shot could be added later, but the tutorial cards already
show the pill.
