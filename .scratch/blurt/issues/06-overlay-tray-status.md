# 06 — Status overlay + tray state feedback

Status: ready-for-human
Type: HITL

## Parent

[.scratch/blurt/PRD.md](../PRD.md)

## What to build

Visual feedback wired to the Pur dictation flow. A small borderless, top-most,
click-through WPF overlay pill anchored to the mouse pointer (or a fixed
bottom-center anchor) that shows "listening…" while recording and "transcribing…"
while processing, then disappears on insert. The tray icon changes
colour/animation in sync (idle → recording → processing). An optional start/stop
sound, off by default.

## Acceptance criteria

- [ ] While holding the key the overlay shows a "listening" state near the pointer/anchor.
- [ ] After release the overlay shows "transcribing", then disappears once text is inserted.
- [ ] The overlay is click-through and never steals focus from the target app.
- [ ] The tray icon visibly reflects idle / recording / processing.
- [ ] Sound is off by default and can be toggled on.

## Blocked by

- 05 — Pur dictation end-to-end
