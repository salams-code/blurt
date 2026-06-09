# PRD — Blurt (Windows Push-to-Talk Voice Dictation)

**Status:** ready-for-agent
**Date:** 2026-06-09
**Design source:** [docs/superpowers/specs/2026-06-09-blurt-windows-design.md](../../docs/superpowers/specs/2026-06-09-blurt-windows-design.md)

## Problem Statement

Typing is slow and high-friction for the user on Windows, especially for quick
messages, notes, and rewrites scattered across many applications. Existing
solutions are either macOS-only (the `blitztext-app` that inspired this), tied to
a single app, or send audio to the cloud by default with no offline option. The
user wants to speak instead of type, get clean text inserted wherever the cursor
is, and stay in control of privacy and cost.

## Solution

Blurt is a native Windows tray application for push-to-talk dictation. The user
holds a global hotkey, speaks, releases, and transcribed — optionally
AI-refined — text is inserted at the current cursor position in whatever app is
focused. Three hotkeys give three behaviors: grammar/cleanup ("Fix"), translate
to English ("English"), and a cycling "Flex slot" that switches between verbatim
("Pur"), bullet points, and a user-defined custom prompt. Transcription runs
locally by default (no audio leaves the PC); only the verbatim "Pur" mode is
guaranteed fully offline. The app is portable (single folder, no installer, no
admin) and fail-soft (a failure never crashes, it falls back and notifies).

## User Stories

1. As a Windows user, I want to hold a global hotkey and dictate, so that I can enter text without typing in any focused application.
2. As a user, I want the transcribed text inserted at my current cursor position, so that it lands exactly where I was working.
3. As a user, I want a "Fix" hotkey (`AltGr + ,`) that cleans up grammar, punctuation, and filler words, so that my spoken text reads like written text.
4. As a user, I want an "English" hotkey (`AltGr + .`) that translates my (German) speech into English, so that I can write English text by speaking German.
5. As a user, I want a "Flex slot" hotkey (`AltGr + -`) whose behavior I can cycle, so that I can reuse one key for several occasional modes.
6. As a user, I want to short-tap the Flex-slot key to cycle its mode (Pur → Bullets → Custom → Pur), so that I can switch behaviors without opening settings.
7. As a user, I want to hold the Flex-slot key to dictate with its currently selected mode, so that tap and hold on the same key do different, predictable things.
8. As a user, I want a "Pur" mode that inserts verbatim transcription with no AI call, so that I have a fully offline, unaltered option.
9. As a user, I want a "Bullets" mode that reformats my dictation into clean bullet points, so that I can quickly produce lists.
10. As a user, I want a "Custom" mode driven by a prompt I define in settings, so that I can tailor one slot to my own recurring task.
11. As a user, I want the tray to show the Flex slot's current mode after each cycle, so that I always know what holding the key will do.
12. As a user, I want a visible "listening" indicator while I hold the key, so that I know the app is capturing my voice.
13. As a user, I want a "transcribing" indicator after I release, so that I know it is processing and not stuck.
14. As a user, I want the indicator to disappear once text is inserted, so that it does not clutter my screen.
15. As a user, I want the status overlay to be quiet and unobtrusive (small, top-most, click-through, near the pointer), so that it never blocks my work or steals focus.
16. As a user, I want transcription to run locally by default, so that my audio never leaves my computer.
17. As a German-primary speaker, I want a multilingual local model so that German dictation works well and English mode can translate it.
18. As a user on a CPU-only laptop (Intel UHD, no GPU), I want transcription tuned for CPU, so that latency stays acceptable without dedicated hardware.
19. As a user, I want to choose the local model size (default `small`, fallback `base`) in settings, so that I can trade accuracy for speed.
20. As a user, I want the option to switch transcription to the OpenAI Whisper API, so that I can get lower latency when I accept sending audio to the cloud.
21. As a user, I want AI refinement to go through one configurable OpenAI-compatible endpoint (base URL + model + key), so that I can use OpenAI now and point it at my own Ollama box later without a new version.
22. As a user, I want `gpt-4o-mini` as the default refinement model, so that rewrites are cheap and fast out of the box.
23. As a user, I want my API key stored encrypted (DPAPI, current user) and never in plaintext, so that another user or a stray file read cannot grab it.
24. As a user, I want non-secret settings stored as readable JSON in `%APPDATA%\Blurt`, so that I can inspect or back them up.
25. As a user, I want the Whisper model downloaded on first run rather than bundled, so that the app download stays small.
26. As a first-time user, I want guided onboarding (mic selection + level test, step-by-step API-key setup, model download, hotkey overview), so that I can get running without prior knowledge.
27. As a user, I want to remap the three hotkeys, so that I can avoid conflicts with other apps and pick keys I find comfortable.
28. As a user, I want to reorder or adjust the Flex-slot mode cycle and edit the Custom prompt, so that the slot fits how I actually work.
29. As a user, I want the tray icon to reflect state (idle → recording → processing), so that I have ambient feedback without an overlay.
30. As a user, I want an optional start/stop sound that is off by default, so that the app is meeting-friendly but can give audible cues if I want.
31. As a user, I want the app to keep running silently in the tray after setup, so that it is always ready and out of the way.
32. As a privacy-conscious user, I want Pur mode to make zero network calls, so that I have a provably offline path.
33. As a privacy-conscious user, I want non-Pur modes to send only text (never audio) to the configured endpoint, so that my voice recording never leaves the machine.
34. As a user, when no microphone is available or permission is denied, I want a clear notice instead of a crash, so that I understand what went wrong.
35. As a user, when transcription fails, I want a notice and nothing inserted, so that I do not get garbage in my document.
36. As a user, when the refinement endpoint is unreachable (e.g. my Ollama box is off), I want the raw transcription inserted plus a notice, so that I still get my words instead of nothing.
37. As a user, when the target app blocks paste/injection, I want my text left on the clipboard plus a notice, so that I can paste it manually and lose nothing.
38. As a user, I want a portable single-folder app I can run without installer or admin rights, so that I can use it on a locked-down or work device.
39. As a user, I want the AltGr special character (e.g. `@ € { [`) suppressed when I use a Blurt hotkey, so that the trigger key never leaks junk characters into my text.
40. As a user, I want tap-vs-hold timing to be configurable, so that I can tune what counts as a quick tap versus a dictation hold.

## Implementation Decisions

- **Platform & stack:** Native Windows app on .NET 8 / C#. WPF for the settings window and the status overlay; WinForms `NotifyIcon` for the tray. Portable build — single folder, run `Blurt.exe` from the user directory, no installer, no admin, no Program Files. No code signing for v1 (accept one-time SmartScreen).
- **Hotkey mechanism:** Use a low-level keyboard hook (`WH_KEYBOARD_LL`), NOT `RegisterHotKey` — the latter only fires on key-down and cannot do push-to-talk (needs key-up) or tap-vs-hold. AltGr is internally `Ctrl + Right-Alt`; detect right Alt (`VK_RMENU`) plus the trigger key. The hook swallows the trigger keystroke so the AltGr special character never reaches the focused app. (Candidate for ADR-0001.)
- **Tap-vs-hold resolution:** Key-down-to-key-up duration under a configurable threshold (~250 ms default) is a tap (cycle the Flex slot); longer is a dictation hold. The pure timing decision is separated from the Win32 hook so it can be reasoned about and tested independently.
- **Two-stage AI pipeline, second stage optional:**
  - **Transcription (`ITranscriber`)** — speech → raw text. Default `LocalWhisper` via Whisper.net (whisper.cpp), multilingual model (not `.en`), default model `small` (q5, ~460 MB, ~2–4 s), fallback `base` (~140 MB, ~1 s). CPU-bound for the target hardware; GPU acceleration auto-enabled only if usable hardware is detected. Alternative `OpenAiWhisper` (online) selectable in settings.
  - **Refinement (`IRefiner`)** — raw text → refined text. A single OpenAI-compatible HTTP client with configurable base URL + model + API key, covering OpenAI cloud and a remote Ollama instance with no code change. Default endpoint OpenAI, model `gpt-4o-mini`. Pur mode skips this stage entirely. (Candidate for ADR-0002: single OpenAI-compatible refiner client.)
- **Mode handling (`ModeRegistry`):** Holds the prompts for Fix / English / Bullets / Custom and the current Flex-slot state. Resolves which mode a hotkey event maps to and whether that mode calls the refiner. Flex-slot order is Pur → Bullets → Custom → Pur.
- **Text injection (`TextInjector`):** Save current clipboard → set clipboard to the result text → simulate `Ctrl+V` via `SendInput` → restore the original clipboard. The text caret position is not reliably queryable across apps, so injection targets the focused app's own cursor via paste rather than caret coordinates.
- **Status feedback:** `Overlay` is a borderless, top-most, click-through WPF pill anchored to the mouse pointer or a fixed screen anchor (bottom-center) — NOT the text caret. States: "listening…" → "transcribing…" → gone on insert. `TrayIcon` color/animation mirrors idle → recording → processing. Optional start/stop sound, off by default.
- **Settings & storage (`SettingsStore`):** Non-secret config as JSON at `%APPDATA%\Blurt\config.json` (transcription mode + model size, refinement base URL + model, hotkey bindings, Flex-slot order, Custom prompt, overlay anchor, sound on/off). API key stored separately, DPAPI-encrypted (`ProtectedData`, current-user scope), never plaintext. Whisper model downloaded to `%APPDATA%\Blurt\models\` on first run.
- **Error handling (fail-soft, `Notifier`):** No mic/permission → tray+overlay notice, no crash. Transcription fails → notice, nothing inserted. Endpoint unreachable → fall back to raw text + notice. Paste blocked → leave text on clipboard + notice.
- **Onboarding flow:** (1) mic selection + level test, (2) step-by-step OpenAI API-key guide + entry stored via DPAPI, (3) download Whisper `small`, (4) show hotkey bindings with remap option. Then run silently in the tray.
- **Component boundaries:** `AppHost`/`TrayIcon`, `HotkeyManager`, `AudioRecorder` (NAudio), `ITranscriber`, `IRefiner`, `ModeRegistry`, `TextInjector`, `Overlay`, `SettingsStore`, `SettingsWindow`, `Notifier` — each with a single responsibility and a defined interface.

## Testing Decisions

**What makes a good test here:** assert observable external behavior at the highest available seam, not implementation details. Exercise real collaborators where cheap (e.g. a mock HTTP server) and fake only the boundaries that are hardware/OS/network-bound. No test asserts private internals or exact prompt strings beyond the behavior they produce.

Headless, automated seams (follow TDD where feasible — there is no prior art yet; this codebase is greenfield, so these tests establish the patterns):

- **Refinement seam (HTTP boundary):** Drive `IRefiner` with (raw text, mode) against a mock OpenAI-compatible server. Covers per-mode prompt construction, request/response handling, and the "endpoint unreachable → raw text fallback" rule. Do not mock the client; mock the server.
- **Pipeline orchestration seam:** Drive the record → transcribe → (optionally refine) → inject flow with a fake `ITranscriber`, fake `IRefiner`, and fake `TextInjector`. Asserts Pur skips the refiner, non-Pur modes invoke it, and failures fall back softly.
- **Mode / Flex-slot seam:** Test `ModeRegistry` directly for the Pur → Bullets → Custom → Pur cycle, current-mode resolution, and which mode maps to which prompt / whether it calls the refiner.
- **Tap-vs-hold seam:** Test the extracted pure timing-decision function with synthetic down/up timestamps against the configurable threshold.
- **Settings seam:** Test `SettingsStore` config JSON round-trip and the API-key DPAPI encrypt/decrypt round-trip. (DPAPI is Windows-only, so this runs on the Windows side.)

Manual-only (hardware/OS-bound, not automatable): the `WH_KEYBOARD_LL` hook, microphone capture (`AudioRecorder`), `SendInput` injection (`TextInjector`), and overlay placement/DPI.

Build and test of the GUI, hotkeys, and injection happen on the Windows side (`dotnet`); they cannot be exercised from WSL. Source is edited in the repo.

## Out of Scope

Explicitly not in v1: emoji mode, anger-defuser mode, fixed email/compact modes (covered by Custom), user accounts, a backend server, code signing, and an installer. Reusing any code from the macOS `blitztext-app` (Swift/WhisperKit/CoreML, Apple-only) — only the concept carries over; this is a new codebase.

## Further Notes

- Primary spoken language is German; the English mode translates German speech to English, which is why the local model must be multilingual, not an English-only build.
- The single OpenAI-compatible refiner client is the key extensibility decision: switching from OpenAI cloud to a remote Ollama box is a settings change (point base URL at e.g. `http://<host>:11434/v1`), not a code change.
- Low-level keyboard hooks can attract endpoint-security (EDR) attention in corporate environments. Acceptable here — the device policy permits private, self-installed tools.
- Open items to resolve during planning/implementation: concrete .NET solution/project layout (app, core lib, tests), Whisper.net CPU runtime package selection and model-download UX, overlay anchoring (mouse vs fixed) and DPI handling, and the exact mode prompts (German output for Fix, English translation, bullets).
