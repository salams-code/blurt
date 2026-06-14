# 44 — Settings status line: effective transcription backend

Status: ready-for-agent

## Parent

[docs/adr/0001-ship-vulkan-whisper-runtime.md](../../../docs/adr/0001-ship-vulkan-whisper-runtime.md)

## What to build

Show the *effective* backend under the GPU-acceleration toggle in Settings, e.g.
"Aktiv: GPU (Vulkan)" or "Aktiv: CPU — keine kompatible GPU/Treiber gefunden". Driven by the
active-backend signal from issue 43. Read-only — it reports, it doesn't decide.

## Acceptance criteria

- [ ] Pure `StatusText(setting, probeResult) → text`, unit-tested (RED first), covering GPU-active, CPU-fallback, and GPU-off.
- [ ] The Settings window shows the status line and reflects the real active backend.
- [ ] Suite stays green.

## Blocked by

- [Issue 43](43-vulkan-warmup-probe-fallback.md)
