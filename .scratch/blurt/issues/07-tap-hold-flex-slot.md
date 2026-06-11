# 07 — Tap-vs-hold detection + Flex-slot cycling

Status: ready-for-human
Type: AFK core (timing logic + slot state are unit-tested) / HITL feel check

## Implementation note (handoff)

Two pure, hook-free Core types, fully unit-tested:

- `TapHoldClassifier` — `TapHoldClassifier(TimeSpan? threshold = null)` (default
  `250 ms`, exposed as static `DefaultThreshold` / instance `Threshold`);
  `TapOrHold Classify(TimeSpan heldDuration)`. Boundary is exact:
  `< threshold` = `Tap`, `>= threshold` = `Hold`. New `enum TapOrHold { Tap, Hold }`.
  5 tests: below = tap, above = hold, exactly-at-threshold = hold, the 250 ms
  default, and a configured threshold reclassifying the same duration.
- `FlexSlotCycle` — `FlexSlotCycle(IReadOnlyList<FlexSlotMode>? order = null)`
  (default `BlurtConfig.Default.FlexSlotOrder`, i.e. Pur → Bullets → Custom);
  `FlexSlotMode Current`; `FlexSlotMode Cycle()` advances one step (wrapping) and
  returns the new current. Reuses the **existing** `FlexSlotMode` enum / order —
  nothing redefined. Empty order throws `ArgumentException`. 5 tests: starts at
  the first mode, advances + wraps through the default order, respects a custom
  order, single-element order, empty-order rejection. (Deliberately **not** named
  `ModeRegistry` — that name is reserved for the issue 11 merge.)

Tray glue in `TrayApplicationContext` (manual feel-check, not unit-tested): the
Flex-slot trigger is dispatched to a new `OnFlexSlotTrigger(KeyEdge)`. Down
stamps `Environment.TickCount64` and starts recording (tray shows
`<mode> (recording)`); up measures the held duration and asks
`TapHoldClassifier`. **Tap** discards the take and `FlexSlotCycle.Cycle()`s,
showing the new mode in the tray text + a short balloon. **Hold** stops
recording and, if the current mode is `Pur`, dictates verbatim via the existing
`DictationPipeline` (no refine) — reusing `DictateAsync`, the same Pur path as
the English trigger. A hold in `Bullets`/`Custom` shows "<mode> mode not
available yet." (those modes are wired in issue 11). English-, Fix-trigger and
the existing Pur path are untouched. 52 Core tests pass; `build src/Blurt.App`
green.

Remaining = the manual feel-check below, to run from the native Windows folder:
1. Run `Blurt.exe`. Tap `AltGr + -` briefly: the tray tooltip/balloon shows the
   next slot mode. Tap repeatedly and watch it cycle Pur → Bullets → Custom →
   Pur …
2. With the slot on **Pur**, hold `AltGr + -`, speak a German sentence, release:
   the transcribed text is injected at the cursor (same path as `AltGr + .`).
3. Tap to **Bullets** (or **Custom**), then hold and speak: nothing is injected;
   a balloon says "<mode> mode not available yet." (lands in issue 11).
4. A very short hold (under ~250 ms) counts as a tap, not a dictation — no audio
   is injected.

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

- [x] Unit tests: durations below the threshold resolve to "tap", above to "hold", with the boundary configurable.
- [x] Unit tests: tapping cycles Pur → Bullets → Custom → Pur → … and current-mode resolution is correct.
- [ ] Holding the Flex-slot key dictates using the current mode (Pur path functional end-to-end). *(wired via DictationPipeline; manual feel-check pending)*
- [x] The tray shows the current slot mode after each tap-cycle.
- [x] The threshold is configurable.

## Blocked by

- 05 — Pur dictation end-to-end
