# 45 — Driver-missing nudge (one-time, dismissible)

Status: ready-for-agent

## Parent

[docs/adr/0001-ship-vulkan-whisper-runtime.md](../../../docs/adr/0001-ship-vulkan-whisper-runtime.md)

## What to build

When the active display adapter is **"Microsoft Basic Display Adapter"** (Windows' fallback
when the real GPU driver is missing) **and** Vulkan did not load, show a **one-time,
dismissible** tray notice suggesting the user install/repair their graphics driver for faster
transcription. Every other "no Vulkan" case shows only the Settings status line (issue 44) —
no popup. Dismissal persists across launches.

This is the deliberately **conservative** trigger from ADR-0001: high confidence, low false
positives. A named AMD/NVIDIA/Intel GPU that simply lacks Vulkan must NOT fire the nudge.

## Acceptance criteria

- [ ] Pure decision `(driverMissingSignal, vulkanLoaded, alreadyDismissed) → showNudge`, unit-tested (RED first), all cases.
- [ ] WMI detection of the "Microsoft Basic Display Adapter" active-adapter signal (impure shell, App layer).
- [ ] One-time dismissible tray notice via the existing `Notifier`; dismissal persisted in config.
- [ ] Named-GPU-but-no-Vulkan does NOT fire the nudge (only the status line covers it).
- [ ] Suite stays green.

## Note

Live verification of the exact trigger needs a machine with a missing driver — treat that as
opportunistic. The decision logic is unit-covered regardless.

## Blocked by

- [Issue 43](43-vulkan-warmup-probe-fallback.md)
