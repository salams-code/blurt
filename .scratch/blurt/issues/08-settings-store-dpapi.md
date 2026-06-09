# 08 — SettingsStore: JSON config + DPAPI-encrypted API key

Status: ready-for-agent
Type: AFK

## Parent

[.scratch/blurt/PRD.md](../PRD.md)

## What to build

A `SettingsStore` that persists non-secret configuration as readable JSON at
`%APPDATA%\Blurt\config.json` (transcription mode + model size, refinement base
URL + model, hotkey bindings, Flex-slot order, Custom prompt, overlay anchor,
sound on/off) and stores the API key separately, DPAPI-encrypted
(`ProtectedData`, current-user scope), never in plaintext. No settings UI in this
slice — just the store with a clean API and round-trip behaviour. This unblocks
refinement (needs the key) and the settings window.

## Acceptance criteria

- [ ] Unit test: a config object written then read back is equal (JSON round-trip), with sensible defaults when the file is missing.
- [ ] Unit test: an API key encrypted then decrypted round-trips to the original value (DPAPI, current user) — runs on the Windows side.
- [ ] The stored API key file is not human-readable plaintext.
- [ ] Config and secret are stored in separate locations under `%APPDATA%\Blurt`.

## Blocked by

- 01 — Solution skeleton + tray that runs
