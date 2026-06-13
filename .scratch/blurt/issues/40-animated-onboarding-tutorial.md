# 40 — Animated onboarding tutorial (teach first-run users how to drive Blurt)

Status: ready-for-human
Type: HITL design + implementation

## Parent

[.scratch/blurt/PRD.md](../PRD.md)

## What to build

Right now onboarding is a setup wizard (mic → privacy tier → API key → done). It
configures the app but never **teaches** it. A first-run user is left to discover
push-to-talk, the three hotkeys, and the Flex tap/hold-and-modes on their own. Add
a teaching step so a newcomer actually learns to use Blurt — clearly presented and
**animated**, in the same visual language as the status pills.

The teaching covers:
- Push-to-talk: hold the key, speak, release — text lands at the cursor.
- The three triggers (Fix / English / Flex) and what each does.
- Flex specifically: **tap** to cycle the mode, **hold** to dictate; the modes
  (Pur = verbatim & offline, Bullets, Custom, Email).
- That the overlay pill tells you the live status (listening → transcribing →
  fixing/bulleting/…); reuse the real pill so the lesson matches reality.

### Proposed concept (from the design conversation — refine when picked up)

A short **animated coach** appended to onboarding, combining:
1. **Animated explainer cards** — one card per concept, each playing a looping
   micro-animation built from the existing overlay pill (the pulsing dot, the
   ellipsis, the mode-flash colour cycle Pur→Bullets→Custom→Email). The pill we
   already ship becomes the illustration, so what they learn is exactly what they'll
   see.
2. **A "try it" finale** — one live, hands-on step: the real overlay appears and
   asks the user to actually hold a hotkey and speak; it confirms when it worked
   ("that was Fix — you've got it"). Learn-by-doing for the muscle memory.
3. **Replayable** — a "How to use Blurt" entry (tray menu) re-opens this tutorial
   any time, so it isn't a one-shot.

Decision needed (HITL): how far to go on (2) the interactive try-it (full live
capture vs a simulated demo), and the exact card sequence/copy. The animation
mechanism is proven (overlay Storyboard/DispatcherTimer, issue 33).

## Acceptance criteria

- [ ] First run shows a teaching step (after setup) covering push-to-talk, the three
      triggers, and Flex tap/hold + modes.
- [ ] It is animated, reusing the overlay-pill visual language.
- [ ] The tutorial can be replayed later from the tray menu.
- [ ] Skippable; never blocks a returning user.
- [ ] Any pure content/sequence logic lives in `Blurt.Core`, unit-tested; suite green.

## Blocked by

- None — can start immediately (independent of the prompt/hotkey backlog 34–39).

## Notes

Related: the live-status overlay (issue 33) provides the animation building blocks;
the privacy-tier step (issue 27) is part of the existing setup wizard this extends.
