# 43 — Eager warmup-probe → cached factory + robust fallback + active-backend signal

Status: ready-for-agent

## Parent

[docs/adr/0001-ship-vulkan-whisper-runtime.md](../../../docs/adr/0001-ship-vulkan-whisper-runtime.md)

## What to build

Make the backend choice robust and observable. At startup (when `GpuPreference = Auto`),
build the `WhisperFactory` under the preferred backend **in the background**; on success it
**becomes the cached working factory** (no second model load, and it hides the one-time
Vulkan shader-compile cost); on failure rebuild under `[Cpu]`. The probe outcome — *which
backend is actually active* — is exposed for the status line (issue 44) and the driver nudge
(issue 45). A dictation fired while the probe is in flight awaits the in-flight factory rather
than starting a second load.

The crash-proof subprocess probe is explicitly **out of scope** (ADR-0001 names it as a v2
follow-up).

## Acceptance criteria

- [ ] Startup warmup builds the factory off the UI thread; the tray icon appears without waiting on it.
- [ ] On probe success the same factory instance is reused for the first dictation (no double model-load).
- [ ] On probe failure the runtime falls back to a CPU factory and transcription still works.
- [ ] The active backend (GPU/Vulkan vs CPU) is queryable by other components.
- [ ] The pure part of the decision (setting + probe result → order / active-backend) is unit-tested (RED first).
- [ ] A first dictation during an in-flight probe awaits it — no concurrent second load.
- [ ] Suite stays green.

## Blocked by

- [Issue 42](42-gpu-preference-vulkan-backend.md)
