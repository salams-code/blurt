# 03 — Text injection at the cursor via clipboard

Status: ready-for-human (implemented; live paste check pending)
Type: HITL

## Implementation note (handoff)

Headless orchestration lives in `Blurt.Core.TextInjector` and is fully
unit-tested (4 cases: set-text + paste, restore only after the post-paste
delay, paste failure leaves text on clipboard, snapshot failure still pastes).
Seams: `IClipboard` (opaque snapshot/restore, preserves non-text formats) and
`IPasteKeystroke`, plus an injectable post-paste delay so tests never sleep.
Thin Win32 adapters in `Blurt.App`: `WinFormsClipboard` (detached `DataObject`
copy of all formats, STA UI thread) and `SendInputPasteKeystroke` (`SendInput`
Ctrl+V chord). Wired to the **Fix trigger key-up only** with the fixed string
"hello from blurt" (300 ms restore delay); English/FlexSlot wiring belongs to
a parallel issue.

Remaining = the manual check below, to run from the native Windows folder:
1. Copy something distinctive (a known sentence, or an image for the non-text
   case) so the clipboard has prior contents.
2. Run `Blurt.exe`, focus Notepad, tap `AltGr + ,` (Fix) and release →
   "hello from blurt" appears at the caret.
3. Repeat in a second app (e.g. a browser text field) → same text appears.
4. Wait ~half a second, then paste manually (`Ctrl+V`) → the *original*
   clipboard contents from step 1 come back (restore worked).
5. Simulated paste failure: if the Ctrl+V cannot be delivered (e.g. target is
   an elevated/admin window that blocks `SendInput`), nothing is restored —
   "hello from blurt" stays on the clipboard, so a manual `Ctrl+V` still
   produces it (no silent loss).

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

**2026-06-11 (agent):** Live check found the flagged AltGr follow-up is the
common case, not an edge: releasing the trigger key while still holding AltGr
made the target app see Ctrl+Alt+V → no paste ("worked the first time, then
never again" depending on release order). Chord composition extracted to
`Blurt.Core.PasteChord` (unit-tested): physically held Alt keys are released
ahead of the Ctrl+V chord, all in one SendInput call. Release order no longer
matters.
