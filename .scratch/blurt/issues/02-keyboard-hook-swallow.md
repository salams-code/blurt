# 02 — Keyboard hook fires and swallows the AltGr trigger character

Status: done
Type: HITL

## Implementation note (handoff)

Headless decision logic lives in `Blurt.Core.TriggerResolver` and is fully
unit-tested (8 cases: trigger down/up, swallow, AltGr-released-first, the three
bindings, right-Alt and non-trigger pass-through). The Win32 plumbing is a thin
adapter `Blurt.App.KeyboardHook` (`WH_KEYBOARD_LL` → resolver → swallow), wired
into the tray with a visible down/up signal (icon swap + balloon).

Remaining = the manual check below, to run from the native Windows folder:
1. Run `Blurt.exe`, focus Notepad.
2. Hold `AltGr + ,` → no comma appears; tray shows "Fix (down)". Repeat for
   `AltGr + .` (English) and `AltGr + -` (FlexSlot).
3. Type letters and `AltGr + Q` (=`@`) → these appear normally (pass-through).
4. Tray → Exit, then `AltGr + ,` types a comma again → hook uninstalled cleanly.

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

## Comments

**2026-06-10 (agent):** Live check surfaced a bug: holding a trigger spammed
balloons. Cause: OS auto-repeat `KEYDOWN`s each produced a fresh trigger Down in
`TriggerResolver`. Fixed via TDD — repeat downs of the held trigger key are now
swallowed without a new event (exactly one Down per press, one Up per release),
including after AltGr is released first (that path previously leaked the
character). Two regression tests added; 10/10 green. The balloon itself is the
intended interim signal; issue 06 replaces it.

**2026-06-10 (user):** Live check passed — all three triggers swallow correctly,
pass-through intact, no repeat spam. `AltGr + #` producing nothing is expected
OS behavior (unbound Ctrl+Alt combo, not a Blurt binding); confirmed the spec
defines exactly three hotkeys. Issue closed.
