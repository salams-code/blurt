# 13 — Fail-soft error handling + Notifier

Status: ready-for-human
Type: AFK logic / HITL verification

## Parent

[.scratch/blurt/PRD.md](../PRD.md)

## What to build

A consistent fail-soft layer with a `Notifier` (tray + overlay messages) so no
failure ever crashes the app. Cover the four failure modes from the design:
no mic / permission denied → notice, no crash; transcription fails → notice,
nothing inserted; refinement endpoint unreachable → insert raw text + notice
("refinement offline, raw text inserted"); paste/injection blocked by the target
app → leave text on the clipboard + notice. Consolidate the ad-hoc fallbacks from
earlier slices behind one path.

## Acceptance criteria

- [ ] Tests (where seam allows): endpoint-unreachable inserts raw text + notice; injection-blocked leaves text on clipboard + notice.
- [ ] No-microphone / permission-denied produces a clear notice and the app keeps running.
- [ ] Transcription failure inserts nothing and notifies.
- [ ] Every notice is shown via the `Notifier` (tray + overlay), not a blocking dialog.

## Blocked by

- 09 — Refiner client (OpenAI-compatible) + Fix mode
