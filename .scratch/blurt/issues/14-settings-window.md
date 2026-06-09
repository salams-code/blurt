# 14 — Settings window

Status: ready-for-human
Type: HITL

## Parent

[.scratch/blurt/PRD.md](../PRD.md)

## What to build

A WPF settings window over the `SettingsStore` (08), exposing the configurable
surface: remap the three hotkeys, choose transcription source (local/online) and
local model size (`small`/`base`), set the refinement base URL + model + API key,
edit the Flex-slot mode order and the Custom prompt, set the overlay anchor, and
toggle the start/stop sound. Changes persist via the store and take effect (live
or with a clearly defined restart).

## Acceptance criteria

- [ ] All settings listed are editable in the window and persist across restarts.
- [ ] Remapped hotkeys take effect and the keyboard hook respects them.
- [ ] The API key field writes through the DPAPI-encrypted path, never plaintext.
- [ ] Invalid input (e.g. a conflicting hotkey, malformed base URL) is handled gracefully.

## Blocked by

- 07 — Tap-vs-hold detection + Flex-slot cycling
- 08 — SettingsStore: JSON config + DPAPI-encrypted API key
