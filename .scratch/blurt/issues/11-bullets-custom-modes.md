# 11 — Bullets + Custom modes in the Flex slot

Status: ready-for-agent
Type: AFK

## Parent

[.scratch/blurt/PRD.md](../PRD.md)

## What to build

Complete the Flex-slot modes. "Bullets" reformats the dictation into clean bullet
points via `IRefiner`. "Custom" refines using a user-defined prompt stored in
settings. Both plug into the slot cycle from 07 (Pur → Bullets → Custom) and the
refiner pipeline from 09. Holding the Flex-slot key dictates with whichever of the
three is currently selected; Pur still skips the refiner.

## Acceptance criteria

- [ ] Test: selecting Bullets sends the bullets prompt; selecting Custom sends the stored custom prompt (asserted against the mock server).
- [ ] Holding the Flex-slot key in Bullets produces bullet-point output inserted at the cursor.
- [ ] Holding in Custom uses the prompt from settings.
- [ ] Pur in the same slot still makes zero refiner calls.

## Blocked by

- 07 — Tap-vs-hold detection + Flex-slot cycling
- 09 — Refiner client (OpenAI-compatible) + Fix mode
