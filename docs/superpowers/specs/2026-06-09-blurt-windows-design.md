# Blurt — Voice Dictation for Windows (Design)

**Date:** 2026-06-09
**Status:** Approved design, pending implementation plan
**Working name:** Blurt

## 1. Summary

Blurt is a native Windows tray application for push-to-talk voice dictation.
The user holds a global hotkey, speaks, releases, and the transcribed (and
optionally AI-refined) text is inserted at the current cursor position in
whatever application is focused.

It is a **ground-up reimplementation** inspired by the macOS app
`cmagnussen/blitztext-app` (Swift/SwiftUI + WhisperKit/CoreML, Apple-only — no
code is reusable on Windows) and by the multi-output concept of
comivoice.netlify.app ("same dictation, multiple outputs"). Only the *concept*
carries over; this is a new codebase.

Non-goals for v1 (explicitly out of scope): emoji mode, anger-defuser mode,
fixed email/compact modes (covered by Custom), user accounts, backend server,
code signing, installer.

## 2. Interaction Model

Three global hotkeys, push-to-talk. Defaults (remappable in settings):

| Hotkey | Mode | Behavior |
|--------|------|----------|
| `AltGr + ,` | **Fix** | Hold → dictate → grammar/punctuation/filler-word cleanup → insert |
| `AltGr + .` | **English** | Hold → dictate → translate to English → insert |
| `AltGr + -` | **Flex slot** | **Short tap** = cycle slot mode; **hold** = dictate with current slot mode |

Flex-slot modes (cycled by tapping `AltGr + -`): **Pur → Bullets → Custom → Pur …**
The tray shows the current slot mode after each cycle.

- **Pur** — verbatim Whisper output, no LLM call (only fully offline-capable mode).
- **Bullets** — reformat dictation into clean bullet points (LLM).
- **Custom** — user-defined prompt stored in settings (LLM).

Per dictation: hold key → overlay shows "listening" → release → overlay shows
"transcribing" → text inserted at cursor → overlay disappears.

### Tap-vs-hold detection
Key-down-to-key-up duration `< ~250 ms` = tap (cycle slot mode); longer = dictation.
Threshold configurable.

## 3. Hotkey Mechanism (important technical decision)

`RegisterHotKey` only fires on key-**down**, so it cannot implement push-to-talk
(needs key-up) or tap-vs-hold. Blurt uses a **low-level keyboard hook**
(`WH_KEYBOARD_LL`) instead.

- AltGr is internally `Ctrl + Right-Alt`; the hook detects the **right Alt**
  (`VK_RMENU`) plus the trigger key.
- The hook **swallows** the trigger keystroke so the AltGr special character
  (e.g. `@ € { [`) never leaks into the focused app.
- Note for corporate/EDR environments: low-level keyboard hooks may attract
  endpoint-security attention. Acceptable here — device policy permits private
  use and self-installed tools.

## 4. AI Architecture

Two independent AI stages. Only the second is optional (skipped for Pur).

### 4.1 Transcription (speech → text) — `ITranscriber`
- **Default: local** whisper.cpp via **Whisper.net**.
  - Primary language **German** → multilingual model (not `.en`); the English
    mode translates German speech to English.
  - Target hardware has **Intel UHD integrated graphics, no dedicated GPU** →
    CPU-bound. whisper.cpp is well optimized for CPU (AVX + quantized models).
  - **Default model: `small` (q5 quantized)**, ~460 MB, ~2–4 s latency per
    dictation. **Fallback `base`** (~140 MB, ~1 s) selectable in settings.
  - GPU acceleration (Vulkan/CUDA) auto-enabled only if usable hardware is
    detected; not expected on this device.
- **Optional: online** OpenAI Whisper API (one-click switch in settings). Useful
  if local latency is unacceptable; trades off the offline guarantee of Pur.

### 4.2 Refinement (text → text) — `IRefiner`
- Single **OpenAI-compatible HTTP client**. Configurable **base URL + model +
  API key** — the same client covers OpenAI cloud *and* a remote Ollama
  instance (both expose OpenAI-compatible APIs).
- **Default endpoint:** OpenAI, model **`gpt-4o-mini`** (cheap, fast, strong for
  these rewrite tasks — fractions of a cent per dictation).
- **Later (no code change):** point base URL at a remote Ollama box
  (e.g. `http://<host>:11434/v1`) when the user has a "better machine" available.
- Pur mode skips this stage entirely.

## 5. Components

Each unit has one purpose, a defined interface, and is independently reasoned about.

| Component | Responsibility | Key dependency |
|-----------|----------------|----------------|
| `AppHost` / `TrayIcon` | Tray menu, lifecycle, status feedback | WinForms `NotifyIcon` |
| `HotkeyManager` | `WH_KEYBOARD_LL` hook, key→mode resolution, tap/hold logic, keystroke swallowing | Win32 hook |
| `AudioRecorder` | Mic capture start/stop on key hold | NAudio |
| `ITranscriber` | Speech → raw text | `LocalWhisper` (Whisper.net) / `OpenAiWhisper` |
| `IRefiner` | Raw text + mode prompt → refined text | OpenAI-compatible HTTP |
| `ModeRegistry` | Mode prompts (Fix/English/Bullets/Custom) + current flex-slot state | — |
| `TextInjector` | Save clipboard → set text → simulate `Ctrl+V` (`SendInput`) → restore clipboard | Win32 |
| `Overlay` | Borderless, top-most, click-through status pill near mouse/anchor | WPF |
| `SettingsStore` | Config (JSON) + API key via DPAPI | `ProtectedData` |
| `SettingsWindow` | Configuration UI | WPF |
| `Notifier` | Status messages, fail-soft notices | Tray + Overlay |

## 6. Data Flow

```
Hold hotkey ─▶ AudioRecorder (NAudio)
   release  ─▶ ITranscriber ─▶ raw text
                    │
            mode == Pur? ──yes──▶ TextInjector ─▶ cursor
                    │no
              IRefiner(raw, modePrompt) ─▶ final text ─▶ TextInjector ─▶ cursor
```

- Local Whisper: audio never leaves the PC.
- Pur: **zero** network calls.
- Other modes: only **text** (never audio) is sent to the configured endpoint.

## 7. Settings & Storage

`%APPDATA%\Blurt\config.json` (non-secret):
- transcription: local/online, whisper model size
- refinement endpoint: base URL + model
- hotkey bindings, flex-slot mode order, Custom prompt text
- overlay anchor preference, optional sound on/off

API key: stored **separately, DPAPI-encrypted** (`ProtectedData`, current-user
scope) — never plaintext, readable only by the same Windows user.

Whisper model downloaded to `%APPDATA%\Blurt\models\` on first run (keeps the
distributable small).

## 8. First-Run Onboarding

1. Microphone selection + level test.
2. OpenAI API key: step-by-step guide (platform.openai.com → API keys → create →
   paste) + entry; stored via DPAPI. (User does not yet have a key.)
3. Download Whisper `small` model.
4. Show hotkey bindings with option to remap.

Afterwards the app runs silently in the tray.

## 9. Status Feedback

- **Overlay**: small borderless, top-most, click-through WPF pill near the mouse
  pointer / fixed anchor (bottom-center). States: red "listening…", then
  "transcribing…", disappears on insert. Silent by default.
  - Note: the exact text caret position is not reliably queryable across apps;
    the overlay anchors to the mouse pointer or a fixed screen position, not the
    caret.
- **Tray icon** changes color/animation in sync (idle → recording → processing).
- Optional start/stop sound, off by default (meeting-friendly).

## 10. Error Handling (fail-soft)

| Failure | Behavior |
|---------|----------|
| No mic / no permission | Tray + overlay notice, no crash |
| Transcription fails | Notice, nothing inserted |
| Endpoint unreachable (e.g. Ollama offline) | Fall back to **raw text** + notice "refinement offline, raw text inserted" |
| Paste/injection blocked by target app | Text left **on clipboard** + notice |

## 11. Tech & Distribution

- **.NET 8, C#**; WPF (settings + overlay), WinForms `NotifyIcon` (tray).
- **Whisper.net** (local transcription), **NAudio** (audio), `System.Net.Http`
  (refinement endpoint).
- **Portable**: single folder, `Blurt.exe` run from the user directory — no
  installer, no admin, no Program Files. Global hotkeys do not need admin.
- **No code signing for v1.** Device policy permits private/self-installed tools;
  at worst a one-time SmartScreen "run anyway". Signing is an optional later step.
- Build & test happen on the **Windows side** (`dotnet`); the GUI, hotkeys, and
  injection cannot be exercised from WSL. Source is edited in the repo.

## 12. Testing Strategy

- **Unit-testable (headless):** mode-prompt construction, flex-slot cycle logic,
  config + DPAPI round-trip, OpenAI-compatible client against a mock server,
  tap/hold threshold logic. Follow TDD where feasible.
- **Manual (on Windows):** global hotkeys, microphone capture, text injection,
  overlay placement — hardware/OS-bound, not automatable.

## 13. Open Items for the Implementation Plan

- Concrete .NET solution/project layout (app, core lib, tests).
- Whisper.net runtime package selection (CPU build) and model-download UX.
- Overlay anchoring details (mouse vs fixed) and DPI handling.
- Exact mode prompts (German output for Fix; English translation; bullets).
