# 15 — First-run onboarding

Status: ready-for-human
Type: HITL

## Parent

[.scratch/blurt/PRD.md](../PRD.md)

## What to build

A guided first-run flow so a user with no prior knowledge can get running:
(1) microphone selection + level test, (2) step-by-step OpenAI API-key guide
(platform.openai.com → API keys → create → paste) with entry stored via DPAPI,
(3) download the Whisper `small` model, (4) show the hotkey bindings with an
option to remap. Afterwards the app runs silently in the tray. Onboarding runs
only when configuration/key/model are absent.

## Acceptance criteria

- [ ] On a clean machine (no config, no key, no model) the onboarding flow runs end-to-end.
- [ ] The mic test confirms a working input device with a visible level.
- [ ] The API key entered during onboarding is stored DPAPI-encrypted and used by refinement.
- [ ] The `small` model is downloaded and usable after onboarding.
- [ ] Onboarding does not re-run once setup is complete.

## Blocked by

- 08 — SettingsStore: JSON config + DPAPI-encrypted API key
- 09 — Refiner client (OpenAI-compatible) + Fix mode
