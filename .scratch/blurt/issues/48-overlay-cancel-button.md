# 48 — Overlay cancel affordance (X)

Status: ready-for-human

## Parent

New feature request (this session). Builds on issue 47.

## What to build

While a dictation is transcribing/refining, the overlay shows a **cancel affordance (an X)**
that aborts the in-flight dictation via a per-dictation `CancellationTokenSource`; the pipeline
returns `Cancelled` (issue 47) and the overlay/tray return cleanly to Idle with nothing
injected.

## Open design questions (resolve with a human before/while building — do NOT guess)

- **Overlay interactivity:** can the overlay pill receive mouse clicks today, or is the
  overlay window topmost / transparent / click-through? A clickable X may require making
  (part of) it hit-testable.
- **Anchor behaviour:** the overlay can anchor to the mouse pointer
  (`OverlayAnchor.MousePointer`) — a click target that follows the cursor is awkward. What's
  the behaviour at each anchor (MousePointer vs BottomCenter)?
- **Hotkey alternative:** should we ALSO add a cancel **hotkey** (e.g. Esc) as a robust,
  interaction-model-independent way to abort? Push-to-talk users may not want to mouse over to
  a pill. This may even be the primary affordance, with the X as secondary.

## Acceptance criteria

- [ ] A per-dictation `CancellationTokenSource` is plumbed into `RunAsync`; triggering it cancels the in-flight dictation.
- [ ] The overlay shows a cancel affordance while transcribing/refining; activating it cancels.
- [ ] On cancel nothing is injected and the overlay/tray return to Idle with no error notice.
- [ ] The open design questions above are resolved and recorded in this issue before implementation.
- [ ] Suite stays green; HITL: confirm cancel works live across modes.

## Blocked by

- [Issue 47](47-pipeline-cancel-outcome.md)
