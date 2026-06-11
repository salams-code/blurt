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

## Implementation note (handoff)

**Core (TDD, unit-tested):**
- `BlurtConfig.OnboardingCompleted` (`bool`, default `false`) added to `Settings.cs`,
  including the overridden `Equals` **and** `GetHashCode` so the JSON round-trip
  equality holds (this was the load-bearing gotcha).
- `Onboarding.IsNeeded(BlurtConfig) => !config.OnboardingCompleted` and
  `enum OnboardingStep { Microphone, ApiKey, Model, Hotkeys }` in new `Onboarding.cs`.
  The completion flag is the single source of truth: fresh install has no
  `config.json` → `Load()` returns `Default` (flag false) → needed; the wizard
  persists `true` on Finish → never runs again, even if the API key was skipped.
- 5 new Core tests (134 total, all green): round-trip of the flag both ways via
  `SettingsStore` + fake protector (`SettingsStoreTests`), and `IsNeeded`/step-order
  (`OnboardingTests`).

**App (WPF, compiled + manually verified):**
- `OnboardingWindow.xaml(.cs)` — 4-step wizard with Back / Next / Skip / Finish:
  1. **Microphone:** lists input devices via `WaveInEvent.DeviceCount` /
     `GetCapabilities(i)`; live level bar driven by a short `WaveInEvent` capture
     (peak per buffer, decay timer); capture stopped/disposed on leaving the step
     and on close. No-mic case is fail-soft (finish anyway).
  2. **API key:** step-by-step platform.openai.com guide + `PasswordBox`,
     **skippable**; a typed value is saved via `SettingsStore.SaveApiKey` (DPAPI),
     blank = skip (refinement falls back offline).
  3. **Model:** `ModelProvisioner.IsModelPresent(WhisperModel.Default)` — if present,
     shows "already installed" with a checkmark and **does not re-download**; if
     missing, a Download button runs `EnsureModelAsync` with a progress bar, and a
     failed download is **fail-soft** (Retry / continue; runtime re-attempts on first
     dictation). Model is never fetched in test/build.
  4. **Hotkeys:** shows the current bindings via `HotkeyBinding.Format`, with a note
     to remap later in Settings.
  - Finish persists `config with { OnboardingCompleted = true }` via `SettingsStore.Save`.
- **Start wiring** lives in `TrayApplicationContext` constructor: `_settings.Load()` →
  if `Onboarding.IsNeeded(config)` call `RunOnboarding()` (builds `OnboardingWindow`
  with the shared `SettingsStore` + a `ModelProvisioner` over the real
  `GgmlModelDownloader`, shown via `ShowDialog()` on the WinForms STA thread — no
  second `System.Windows.Application`), then reload the config so a saved key /
  completion flag feeds the rest of start-up. Not needed → straight to the quiet tray
  (today's behaviour). The show is guarded so a broken wizard can never block launch.

**Build/test:** `dotnet build src/Blurt.App` green (0 warnings/0 errors);
`dotnet test tests/Blurt.Core.Tests` green (134/134).

### Manual checks (open)
To test, delete `%APPDATA%\Blurt\config.json` **or** set `OnboardingCompleted` to
`false`, then restart Blurt:
- Wizard appears; microphone step shows a moving level bar when you speak.
- Enter or skip the API key (skip → no key stored, refinement stays offline).
- Model step shows "already installed" (the `small` model is already present on this
  machine), so nothing downloads.
- Finish → app goes quiet in the tray.
- Restart again → **no** wizard (flag persisted).

**Machine note:** on this laptop `%APPDATA%\Blurt\config.json` is currently absent, so
the **next start will show onboarding** — and the model step will report the `small`
model as already present (it is downloaded under `%APPDATA%\Blurt\models\`).
