---
status: accepted
---

# Ship the Vulkan whisper runtime, GPU-on by default with CPU fallback

Benchmarks on target-class hardware (Ryzen 7 4700U with an integrated AMD Vega iGPU)
show the Vulkan whisper.cpp backend is ~3x faster than CPU for local transcription
(small q5_1: ~5.7 s → ~1.7 s on a 15 s clip) with **byte-identical** output. We therefore
bundle `Whisper.net.Runtime.Vulkan` and ship a user-visible **"GPU acceleration: Auto / Off"**
setting that defaults to **Auto** — prefer the GPU, fall back to CPU automatically.

## Decision

- New `BlurtConfig` setting `GpuPreference` (`Auto` = default, `Off`). A pure, unit-tested
  function maps it to Whisper.net's `RuntimeOptions.RuntimeLibraryOrder`: `Auto → [Vulkan, Cpu]`,
  `Off → [Cpu]`. `Auto` is the first enum value, so configs written before this setting
  existed default to GPU-on after upgrade.
- The backend is chosen once at startup by an **eager warmup probe**: build the
  `WhisperFactory` under the preferred backend in the background; on success it *becomes*
  the cached working factory (no second load, and it hides the one-time Vulkan shader-compile
  cost); on failure rebuild under `[Cpu]`. The probe result drives both the Settings status
  line and the driver nudge.
- **Driver nudge** (one-time, dismissible tray notice) fires only on a high-confidence signal:
  the active display adapter is `"Microsoft Basic Display Adapter"` (Windows' fallback when the
  GPU driver is missing) **and** Vulkan did not load. Every other "no Vulkan" case shows only
  the Settings status line — no popup. The trigger is a pure function
  `(driverMissingSignal, vulkanLoaded, alreadyDismissed) → showNudge`; the WMI query that feeds
  it is the impure shell.

## Considered options

- **Bundle size** — one portable with Vulkan bundled (~120 MB, **chosen**) vs. two separate
  CPU/GPU downloads vs. fetching the Vulkan native on demand. Chose the single artifact for
  release/QA/support simplicity; the model (500 MB+) is already a separate first-run download.
- **Fallback robustness** — trust Whisper.net's loader only / in-process startup probe (**chosen**)
  / crash-proof subprocess probe. The in-process probe covers ~99% (loader hardware-probe + our
  Vulkan build-probe) and is a clean, testable decision; the crash-proof subprocess is deferred.
- **Control surface** — automatic-only / hidden override / visible setting (**chosen**, default Auto).

## Consequences

- The portable roughly **doubles** (~65 MB → ~120 MB): `ggml-vulkan-whisper.dll` alone is ~55 MB.
- The Vulkan natives land under `runtimes/vulkan/win-x64/`, **not** the usual
  `runtimes/win-x64/native/` that `--selftest` probes today. The single-file portable extraction
  **and** the `--selftest` probe paths must include this layout; verify on the shipped artifact
  via `Blurt.exe --selftest` (extended to report which backend loaded).
- **Known limitation (deferred):** a Vulkan driver that loads but crashes *during inference*
  cannot be recovered in-process (whisper.cpp loads one native lib per process). Default-on relies
  on Whisper.net's loader probe plus our startup probe. If real-world inference crashes appear, the
  mitigation is a crash-proof `--selftest`-style subprocess probe with a per-driver-version cache —
  a follow-up, not part of this change.
