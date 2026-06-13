# 31 — Flex-slot tap feedback is throttled (mode cycle feels stuck)

Status: done (fixed via TDD, 2026-06-13) — HITL confirm pending

## Parent

[.scratch/blurt/PRD.md](../PRD.md)

## What the user observed (2026-06-13)

On the portable build: a Flex-slot tap cycles the mode the first time, but a quick
second tap appears to do nothing — you have to **wait until the tray notice at the
bottom disappears** before it switches again. "Das muss doch auch anders gehen."

## Confirmed cause (code)

The mode **does** cycle on every tap — `FlexSlotCycle.Cycle()` is synchronous with
no timing gate. The problem is the *feedback*: the tap's only signal was a tray
balloon (`TrayNotifier.ShowBalloonTip`, 3000 ms). Windows **throttles successive
NotifyIcon balloons** — while one is showing, the next is suppressed/queued — so a
rapid second tap updated the state invisibly and the cycle looked frozen. The
displayed mode could also lag the real one.

## What was built

- **Core `FlexSlotOverlay`** ([Overlay.cs](../../../src/Blurt.Core/Overlay.cs)) —
  pure mapping `FlexSlotMode → (label, dot RGB)`, a **distinct label and colour per
  mode** (Pur=green, "• Bullets"=blue, Custom=purple) so the cycled-to mode is
  unambiguous; never the status red/amber or idle grey. 3 tests.
- **Overlay shows the mode instead of a balloon**: `OverlayWindow.SetModeFlash` +
  `OverlayController.FlashMode(mode)` ([OverlayController.cs](../../../src/Blurt.App/OverlayController.cs))
  — instant, repeatable pill with a single-shot ~1.1 s auto-hide timer; back-to-back
  taps re-show the latest mode and restart the timer. An explicit `Show`/`Hide`
  (e.g. a started recording) cancels the pending flash so the pill is never hidden
  out from under a live recording.
- **Wiring** ([TrayApplicationContext.cs](../../../src/Blurt.App/TrayApplicationContext.cs)):
  the tap branch now calls `_overlay.FlashMode(mode)` and rests the tray, dropping
  the throttled balloon (user chose overlay-only feedback).

## Acceptance criteria

- [x] Rapid repeated Flex taps each show the new mode immediately (no waiting).
- [x] Each mode is visually distinct (label + colour).
- [x] A tap-then-hold (start recording) is not hidden by the flash timer.
- [x] Suite stays green (210).
- [ ] HITL: confirm live on the portable that fast cycling reads correctly.
