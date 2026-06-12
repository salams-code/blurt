# 14 — Settings window

Status: done (verify-sweep 2026-06-12)
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

- [x] All settings listed are editable in the window and persist across restarts.
- [x] Remapped hotkeys take effect and the keyboard hook respects them.
- [x] The API key field writes through the DPAPI-encrypted path, never plaintext.
- [x] Invalid input (e.g. a conflicting hotkey, malformed base URL) is handled gracefully.

## Blocked by

- 07 — Tap-vs-hold detection + Flex-slot cycling
- 08 — SettingsStore: JSON config + DPAPI-encrypted API key

## Implementation note (handoff)

### Core (unit-tested, TDD)

- **`HotkeyBinding`** (`src/Blurt.Core/HotkeyBinding.cs`) — pure chord ↔ virtual-key
  translation. `bool TryParse(string text, out int virtualKeyCode)` accepts the
  canonical `"AltGr+,"` form and the bare character (`","`), recognising the three
  trigger characters `, . -` (VK 0xBC / 0xBE / 0xBD); returns `false` + `vk == 0`
  for null/empty/garbage. `string Format(int vk)` renders a known VK back as
  `"AltGr+,"` and falls back to `"VK_0x.."` for unknown codes. Round-trip tested.
- **`TriggerResolver` made configurable** (`src/Blurt.Core/TriggerResolver.cs`) —
  new ctor `TriggerResolver(IReadOnlyDictionary<int, TriggerKind> bindings)` plus
  the parameterless ctor that uses the design defaults (now exposed as
  `static IReadOnlyDictionary<int, TriggerKind> DefaultBindings`). The bindings map
  is per-instance, so a remap installs a fresh resolver. Existing
  `TriggerResolverTests` stay green; added tests for custom-VK resolution and that a
  remap drops the old default key.
- **`HotkeyBindings.ResolveVkMap(BlurtConfig)`** (`src/Blurt.Core/HotkeyBindings.cs`)
  — pure, total VK→trigger map builder. Parses each configured chord; a
  missing/unparseable entry falls back to that trigger's design-default chord, so no
  trigger ever becomes unreachable. Always returns all three triggers.
- **`SettingsValidation`** (`src/Blurt.Core/SettingsValidation.cs`) —
  `SettingsValidationResult Validate(BlurtConfig)` returning a
  `SettingsValidationResult(IReadOnlyList<string> Errors)` (`IsValid` => no errors).
  Detects **hotkey conflicts** (two triggers parsing to the same VK) and a
  **malformed refinement base URL** (not an absolute http/https `Uri`). Collects all
  problems at once.
- **`WhisperModel.Base`** (`src/Blurt.Core/ModelProvisioning.cs`) — added
  `static WhisperModel Base { get; } = new("base", "q5_1")` alongside `Default`
  (`small`/q5_1), so the window can offer `small`/`base` as the local model size.

New Core tests: 38 (HotkeyBinding 8 facts/theories, HotkeyBindings 4,
SettingsValidation 7, TriggerResolver +2). Full Core suite: **129 passed, 0 failed**.

### App (WPF — compiled, manually verified)

- **`SettingsWindow.xaml` / `.xaml.cs`** (`src/Blurt.App/`) — grouped, card-styled
  window: Transcription (source + local model size), Refinement (base URL, model,
  API key), Hotkeys (Fix / English / Flex slot), Flex slot (mode order + custom
  prompt), Overlay & sound (anchor + sound toggle). Loads the current config on
  open. **API key uses a `PasswordBox`**: the stored key is never shown — a
  `"(unchanged)"` placeholder appears when one exists; leaving it writes nothing,
  typing a new value calls `SettingsStore.SaveApiKey` (DPAPI). On Save it runs
  `SettingsValidation.Validate` first; failures show **inline** in a red error panel
  and nothing is persisted (no crash). On success it `Save`s the config, optionally
  `SaveApiKey`s, exposes `SavedConfig`, and closes with `DialogResult == true`.
- **`KeyboardHook` ctor overload** (`src/Blurt.App/KeyboardHook.cs`) —
  `KeyboardHook(TriggerResolver resolver)` so a remap can install a hook built from
  the new bindings; the parameterless ctor keeps the default resolver.
- **`TrayApplicationContext`** (`src/Blurt.App/TrayApplicationContext.cs`) — added a
  **"Settings…"** menu item before "Exit" (single instance: reopening calls
  `Activate()`). The start-up hook is now built from the persisted bindings via
  `InstallHook(config)` → `new TriggerResolver(HotkeyBindings.ResolveVkMap(config))`.
  On a successful save, `ApplySettings` re-wires the runtime: disposes the old hook
  and installs a fresh one (remapped hotkeys live), swaps in a new `OverlayController`
  for the anchor, and updates the sound flag. The WPF window is `Show()`n on the
  shared WinForms STA thread — no second `System.Windows.Application`.

### Live vs. restart

- **Live on save:** hotkey remap (hook re-installed), overlay anchor (controller
  swapped), sound toggle (flag re-read each `PlaySound`), refinement base URL /
  model / API key (the refiner is already rebuilt from settings per dictation).
- **Next launch:** transcription source (local/online) and local model size — the
  transcriber is provisioned once lazily; the new value is persisted now and used on
  the next start. Noted inline in the window.

### Open manual checks (GUI not started here — compile-only)

- Open Settings… from the tray; change each field, Save, restart Blurt → values stick
  (inspect `%APPDATA%\Blurt\config.json`).
- Remap a hotkey (e.g. Fix `AltGr+,` → `AltGr+.`), Save → new chord triggers Fix, the
  old one no longer does (no app restart).
- Set an API key → `%APPDATA%\Blurt\apikey.dat` changes and `config.json` contains no
  plaintext key; clear/leave blank → stored key unchanged.
- Enter a conflicting hotkey (two triggers on the same key) or a malformed base URL
  → inline red errors, nothing saved, no crash.

## Comments

**2026-06-12 (agent, verify-sweep):** Validation, hotkey parsing/conflicts, DPAPI write-through unit-tested; window exercised heavily in the 2026-06-12 HITL session (its findings became issues 20/23/24 - all fixed) and re-verified today after the issue-19 redesign (open, resize, provider switch via UIA). Key survives provider switch was HITL-verified under issue 17.
