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

## Findings & proposed resolutions — PENDING HUMAN CONFIRMATION (do not build yet)

Foundation status: **issue 47 is DONE** — `DictationOutcome.Cancelled` exists, the
pipeline treats `OperationCanceledException` as a clean cancel (no inject, distinct
from `TranscriptionFailed`/`RefinedOffline`), and `DictationNotices.For(Cancelled)`
is silent. So the *pipeline* side is ready; what remains is the trigger + UI, which
is where the open questions bite.

Investigated the overlay to ground the questions (not guessed):

- **Overlay interactivity (BLOCKER):** the pill is genuinely click-through today.
  [OverlayWindow.xaml.cs:142](../../../src/Blurt.App/OverlayWindow.xaml.cs#L142) sets
  `WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW`, so hit-tests fall through
  to the window beneath and the overlay never activates. A clickable X therefore
  needs the window (or a sub-region) made hit-testable *while transcribing/refining*
  and reverted afterwards — non-trivial, and it risks stealing focus from the app the
  user is dictating into.
- **Anchor behaviour:** with `OverlayAnchor.MousePointer` the pill is positioned at
  the live cursor on show ([OverlayController.cs:134](../../../src/Blurt.App/OverlayController.cs#L134)),
  so a click target there chases the cursor — awkward/unreliable. With
  `BottomCenter` the pill is a fixed, clickable location.

**Proposed resolution (for confirmation):**
1. Make a **cancel hotkey (Esc) the PRIMARY affordance** — interaction-model-
   independent, works at every anchor, and matches push-to-talk (no mousing to a
   pill). This is plumbing a per-dictation `CancellationTokenSource` + an Esc handler
   in the existing keyboard hook; no overlay-interactivity change required.
2. Treat the **clickable X as SECONDARY**, only at `BottomCenter` (where a fixed
   target makes sense); skip it at `MousePointer`. Defer it unless wanted, since it
   needs the click-through/hit-test rework above.

These are proposals only — **not implemented**. Confirm (1)/(2), the Esc key choice,
and whether the X is in-scope for v1, then this issue is ready to build on top of 47.

## Blocked by

- [Issue 47](47-pipeline-cancel-outcome.md)
