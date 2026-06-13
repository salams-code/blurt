# 33 — Overlay shows precise live status (per-phase, per-mode, animated)

Status: done (built via TDD, 2026-06-13) — HITL confirm pending

## Parent

[.scratch/blurt/PRD.md](../PRD.md)

## What the user asked (2026-06-13)

The overlay should say exactly what's happening, not a generic "busy": local
transcription should read as local; Bullets should say "bulleting", Fix "fixing",
etc. And it should be nicely animated, so the user always sees the current step.

## What was built

- **Core `StatusLabel`** ([Overlay.cs](../../../src/Blurt.Core/Overlay.cs)) — one
  source of truth for the live verbs (lowercase, no trailing "…" so the overlay
  animates the ellipsis): `listening`; `transcribing` / **`transcribing locally`**
  (the local/cloud distinction the user asked for — `Transcribing(bool local)`);
  `fixing`, `bulleting`, `translating`, `refining`. 3 tests.
- **Animated overlay** ([OverlayWindow.xaml.cs](../../../src/Blurt.App/OverlayWindow.xaml.cs)):
  `SetActive` starts a **pulsing dot** (opacity breathe, forever) + a **cycling
  ellipsis** (DispatcherTimer, dots padded to constant width so the pill doesn't
  jiggle). `StopAnimations` restores a steady dot for the mode flash and on hide.
- **Controller** ([OverlayController.cs](../../../src/Blurt.App/OverlayController.cs)):
  `ShowActive(label, colorState)` (positions + animates; red=listening,
  amber=transcribing/refining) and `UpdateActive(label, colorState)` (change the
  label mid-operation without moving the pill). `Hide` stops the animations.
- **Wiring** ([TrayApplicationContext.cs](../../../src/Blurt.App/TrayApplicationContext.cs)):
  `EnterRecording` → "listening"; `EnterProcessing(label)` → the transcription
  verb; the refine delegate calls `UpdateActive(refiningLabel, …)` as soon as
  transcription finishes, so the pill steps transcribing → fixing/bulleting/…
  Each trigger passes its verb: Fix→fixing, English→translating, Flex/Bullets→
  bulleting, Flex/Custom→refining. Pur (DictateAsync, always local) → "transcribing
  locally".

## Pur is local by contract (clarified with the user)

Pur runs only via the Flex slot and goes through `DictateAsync` with
`zeroNetwork: true`, which `TranscriberResolver` hard-gates to local **regardless**
of the configured transcription source — it can never reach the API. Decision:
keep the gate (it's PrivacyTier 0's guarantee, issue 27). The new label makes it
visible: Pur always shows "transcribing locally".

## Acceptance criteria

- [x] The overlay names the current phase and mode (per-verb), not a generic busy.
- [x] Local vs cloud transcription is distinguished in the label.
- [x] The pill is animated while active (pulse + ellipsis) and steady for a flash.
- [x] Suite stays green (213).
- [ ] HITL: confirm the labels/animation read well live across all modes.
