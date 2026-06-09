# 02 — Keyboard hook fires and swallows the AltGr trigger character

Status: ready-for-human
Type: HITL

## Parent

[.scratch/blurt/PRD.md](../PRD.md)

## What to build

A low-level keyboard hook (`WH_KEYBOARD_LL`) that detects a Blurt trigger
(right Alt / `VK_RMENU` plus a trigger key, e.g. `AltGr + ,`) on both key-down
and key-up, and swallows the trigger keystroke so the AltGr special character
never reaches the focused application. This is the input foundation for
push-to-talk; no recording or transcription yet — just reliable, leak-free key
detection with a visible signal (e.g. a tray/console notice) on down and up.

`RegisterHotKey` is explicitly NOT used (it only fires on key-down and cannot do
push-to-talk or tap-vs-hold). Candidate for ADR-0001.

## Acceptance criteria

- [ ] Pressing the configured trigger fires distinct key-down and key-up events the app can observe.
- [ ] The AltGr special character that the trigger would normally type does NOT appear in the focused app.
- [ ] Non-trigger keystrokes pass through untouched.
- [ ] The hook installs and uninstalls cleanly with app lifecycle (no leaked hook on exit).

## Blocked by

- 01 — Solution skeleton + tray that runs
