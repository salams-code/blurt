# 42 â€” GpuPreference setting + Vulkan backend selection with CPU fallback

Status: done (2026-06-14 — shipped in PR #4)

## Parent

[docs/adr/0001-ship-vulkan-whisper-runtime.md](../../../docs/adr/0001-ship-vulkan-whisper-runtime.md)

## What to build

Add a user-visible GPU-acceleration preference and make local whisper transcription
use the Vulkan backend when available, falling back to CPU automatically. This is the
tracer bullet: setting â†’ runtime order â†’ accelerated transcription.

- New `BlurtConfig` setting `GpuPreference` with values **`Auto` (first â†’ default)** and `Off`.
- A pure function maps it to Whisper.net's `RuntimeOptions.RuntimeLibraryOrder`:
  `Auto â†’ [Vulkan, Cpu]`, `Off â†’ [Cpu]`. Set **once at startup, before the first
  `WhisperFactory`** (the order is global-static).
- Reference `Whisper.net.Runtime.Vulkan` (same version as the existing Whisper.net, 1.9.1).
- This slice may rely on Whisper.net's built-in loader auto-fallback (it probes hardware +
  drivers and drops to CPU when Vulkan is unavailable). The explicit warmup-probe and
  robustness live in issue 43.

`Auto` must be the first enum value so configs written before this setting existed default
to GPU-on after an upgrade.

## Acceptance criteria

- [ ] `BlurtConfig.GpuPreference` exists (`Auto` default, `Off`); round-trips through config.json; an absent field resolves to `Auto`.
- [ ] Pure `OrderFor(GpuPreference) â†’ List<RuntimeLibrary>`: `Auto â†’ [Vulkan, Cpu]`, `Off â†’ [Cpu]` â€” unit-tested in `tests/Blurt.Core.Tests` (observe RED first).
- [ ] `RuntimeOptions.RuntimeLibraryOrder` is set from config at startup, before any `WhisperFactory` is created.
- [ ] `Whisper.net.Runtime.Vulkan` referenced in `Blurt.App.csproj`.
- [ ] On a Vulkan-capable machine local transcription runs on the GPU (verifiably faster); on a CPU-only machine it still transcribes (auto-fallback).
- [ ] Test suite stays green.

## TDD note

`OrderFor` + the config default are the pure red-green units. The startup wiring is App shell
â€” verify by running the app / `--selftest`, not by a unit test.

## Blocked by

None â€” can start immediately.
