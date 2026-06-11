# 08 — SettingsStore: JSON config + DPAPI-encrypted API key

Status: ready-for-human
Type: AFK

## Implementation note (handoff)

All logic landed in `Blurt.Core` (headless, unit-tested); `Blurt.App` untouched.

Types (`src/Blurt.Core/`):
- `BlurtConfig` (`Settings.cs`) — `record` holding every non-secret field from
  spec §7: `Transcription` (`TranscriptionMode` Local/Online), `WhisperModel`
  (defaults to `small`/q5_1 via the existing `WhisperModel.Default`),
  `RefinementBaseUrl` (`https://api.openai.com/v1`), `RefinementModel`
  (`gpt-4o-mini`), `HotkeyBindings` (`TriggerKind` → chord string, default
  `AltGr+, . -`), `FlexSlotOrder` (`Pur → Bullets → Custom`), `CustomPrompt`,
  `OverlayAnchor` (MousePointer/BottomCenter), `SoundEnabled` (false).
  `BlurtConfig.Default` is the all-defaults instance. Equality is overridden so
  the collection members (`HotkeyBindings`, `FlexSlotOrder`) compare
  structurally — otherwise a JSON round-trip would never be "equal".
- `ISecretProtector` (`Settings.cs`) — narrow `Protect`/`Unprotect` byte seam.
- `DpapiSecretProtector` (`DpapiSecretProtector.cs`) — real DPAPI
  (`ProtectedData`, `DataProtectionScope.CurrentUser`),
  `[SupportedOSPlatform("windows")]`. Needs NuGet
  `System.Security.Cryptography.ProtectedData` 8.0.0 (added to
  `Blurt.Core.csproj`).
- `SettingsStore` (`SettingsStore.cs`) — injected `appDataRoot` + an
  `ISecretProtector` (same seam pattern as `ModelProvisioner`). `Load()`/
  `Save(BlurtConfig)` for the JSON config; `SaveApiKey(string)`/`LoadApiKey()`
  for the encrypted key. Creates the directory up front.

File layout under `%APPDATA%\Blurt`:
- `config.json` — indented, human-readable JSON; enums serialised by name
  (`JsonStringEnumConverter`).
- `apikey.dat` — DPAPI ciphertext, a **separate** file; never plaintext.

Tests (`tests/Blurt.Core.Tests/`): 9 new (`SettingsStoreTests` ×7 with a
reversible byte-flip fake protector — defaults-when-missing, config round-trip,
readable JSON at the expected path, key round-trip, null-before-save, key file
not plaintext, config/secret are separate paths; `DpapiSecretProtectorTests` ×2,
Windows-guarded — real DPAPI round-trip + ciphertext ≠ plaintext). Full Core
suite green: 31 passed (22 baseline + 9).

Fully automated — no manual check required. Optional sanity: run the app once a
later slice wires `SettingsStore` in, then inspect `%APPDATA%\Blurt\config.json`
(readable) and `apikey.dat` (binary, no plaintext key).

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

- [x] Unit test: a config object written then read back is equal (JSON round-trip), with sensible defaults when the file is missing.
- [x] Unit test: an API key encrypted then decrypted round-trips to the original value (DPAPI, current user) — runs on the Windows side.
- [x] The stored API key file is not human-readable plaintext.
- [x] Config and secret are stored in separate locations under `%APPDATA%\Blurt`.

## Blocked by

- 01 — Solution skeleton + tray that runs
