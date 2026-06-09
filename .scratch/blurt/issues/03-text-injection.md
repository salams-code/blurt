# 03 — Text injection at the cursor via clipboard

Status: ready-for-human
Type: HITL

## Parent

[.scratch/blurt/PRD.md](../PRD.md)

## What to build

A `TextInjector` that inserts a given string at the current cursor position in
whatever app is focused, by: saving the current clipboard → setting the clipboard
to the text → simulating `Ctrl+V` via `SendInput` → restoring the original
clipboard. Triggered for now by any hotkey with a fixed test string (e.g.
"hello from blurt"). The caret position is not queried; insertion relies on the
focused app's own cursor via paste.

## Acceptance criteria

- [ ] Triggering injection pastes the fixed text at the cursor in a normal text field (e.g. Notepad, browser).
- [ ] The user's original clipboard contents are restored afterwards.
- [ ] Injection works across at least two different target applications.
- [ ] If paste cannot complete, the text remains on the clipboard (no silent loss).

## Blocked by

- 01 — Solution skeleton + tray that runs
