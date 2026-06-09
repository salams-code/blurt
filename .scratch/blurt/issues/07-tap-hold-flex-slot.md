# 07 — Tap-vs-hold detection + Flex-slot cycling

Status: ready-for-agent
Type: AFK core (timing logic + slot state are unit-tested) / HITL feel check

## Parent

[.scratch/blurt/PRD.md](../PRD.md)

## What to build

The Flex-slot key (`AltGr + -`) behaviour. A pure timing-decision function maps
key-down-to-key-up duration to {tap | hold} against a configurable threshold
(~250 ms default): a tap cycles the slot mode, a hold dictates with the current
mode. A `ModeRegistry`-backed slot state cycles Pur → Bullets → Custom → Pur and
exposes the current mode; the tray shows the current mode after each cycle. For
this slice only Pur is wired to actually dictate (Bullets/Custom land in 11);
cycling through all three and the tap/hold split must work.

The timing decision and the cycle state are extracted as pure logic so they are
unit-testable without the Win32 hook.

## Acceptance criteria

- [ ] Unit tests: durations below the threshold resolve to "tap", above to "hold", with the boundary configurable.
- [ ] Unit tests: tapping cycles Pur → Bullets → Custom → Pur → … and current-mode resolution is correct.
- [ ] Holding the Flex-slot key dictates using the current mode (Pur path functional end-to-end).
- [ ] The tray shows the current slot mode after each tap-cycle.
- [ ] The threshold is configurable.

## Blocked by

- 05 — Pur dictation end-to-end
