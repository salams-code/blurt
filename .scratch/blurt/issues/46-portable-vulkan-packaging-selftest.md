# 46 — Portable single-file packaging + --selftest reports backend

Status: ready-for-human

## Parent

[docs/adr/0001-ship-vulkan-whisper-runtime.md](../../../docs/adr/0001-ship-vulkan-whisper-runtime.md)

## What to build

Ensure the **single-file portable** bundles and correctly **loads** the Vulkan natives, and
extend `--selftest` to report which backend actually loaded.

The Vulkan natives land under `runtimes/vulkan/win-x64/` — **not** the usual
`runtimes/win-x64/native/` that [SelfTest.cs](../../../src/Blurt.App/SelfTest.cs) probes
today. Both the publish step and the native-load probe paths must include this layout. Per
CLAUDE.md this must be verified on the **shipped artifact** (single-file self-extract), not
just a normal build — the same native-loading lesson as whisper.cpp.

## Acceptance criteria

- [ ] `--selftest` reports which backend loaded (CPU vs Vulkan) and knows the `runtimes/vulkan/win-x64/` path; PASS/FAIL/SKIP + exit codes 0/1/2 preserved.
- [ ] The single-file portable (CLAUDE.md publish recipe) includes the Vulkan natives and loads them — verified via `Blurt.exe --selftest` on the published artifact (`Start-Process -Wait`).
- [ ] Portable size growth is as expected per ADR-0001 (~+55 MB → ~120 MB total) and noted.
- [ ] On a Vulkan box the portable transcribes on the GPU; a CPU-only box falls back.

## HITL

Requires building the portable and running `--selftest` on the published artifact, plus
judgment on the result — not unit-testable.

## Blocked by

- [Issue 42](42-gpu-preference-vulkan-backend.md)
