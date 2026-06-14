# Idea / future spike — Local refinement via in-process LLM (LLamaSharp + Gemma)

Status: idea — NOT scheduled, NOT designed (no grilling yet). Captured so it isn't lost.

## The idea

Replace (or sit alongside) the external **Ollama** dependency for the *refinement* step with an
**in-process** inference engine — **LLamaSharp** (the C#/.NET bindings for `llama.cpp`) — running
a small multilingual model. This removes the "user must install and run Ollama as a separate
process" friction and makes the fully-local privacy tier genuinely self-contained, fitting the
existing `whisper.cpp` native bundling.

This is the *refinement* counterpart to the shipped Vulkan work on *transcription*
(see [docs/adr/0001-ship-vulkan-whisper-runtime.md](../../../docs/adr/0001-ship-vulkan-whisper-runtime.md)).

## Why it's a clean fit

- **The seam already exists.** Everything goes through `IRefiner.RefineAsync`
  ([src/Blurt.Core/Refiner.cs](../../../src/Blurt.Core/Refiner.cs)). Today `OpenAiCompatibleRefiner`
  serves *both* the OpenAI cloud and a remote/local Ollama (same Chat Completions protocol; the
  difference is only whether the API key is sent).
- Adding a third implementation `LLamaSharpRefiner : IRefiner` is **additive**, behind the same
  interface. A small `RefinerResolver` would pick the implementation by provider — mirroring the
  existing `TranscriberResolver` ([src/Blurt.Core/Transcription.cs](../../../src/Blurt.Core/Transcription.cs)).
- Add a third `RefinementProvider` value (e.g. `LocalInProcess`); `RefinerAuth.KeyToSend` → never
  send a key (like Ollama). All three (OpenAI cloud / Ollama / embedded) then coexist.

## The one real wrinkle

The HTTP refiners are rebuilt per dictation (cheap). An in-process LLM is **not** — the multi-GB
GGUF must be **loaded once and cached** (reloaded only when the model path changes). The resolver
must hand back a cached, long-lived instance for the embedded provider.

## Model choice

- **Gemma E2B (~3 GB Q4)** or **E4B (~5 GB Q4)** — built for on-device, strong German, GGUF,
  runs via `llama.cpp`/LLamaSharp, Vulkan backend possible.
- **Not gpt-oss** (smallest is 20B, ~12–16 GB, too big) and **not** "thinking"/reasoning models —
  the refine task (clean up, bullets, email) wants a plain instruct model, not chain-of-thought.

## What we already know (from this session's benchmarking)

- On the target iGPU (Ryzen 7 4700U + integrated AMD Vega), Vulkan gave **whisper** ~3.3× over CPU.
  **But** LLM *decode* is memory-bandwidth-bound and the iGPU shares the DDR4 bus, so that speedup
  will **NOT** transfer 1:1 — refinement latency on this hardware is **unmeasured**.
- Accuracy test (78 s German clip, known reference): local whisper small ≈ 98 %, OpenAI cloud
  ≈ 100 %; the residual local errors (e.g. "automasisch", "verschieben wird") are exactly the
  kind a Fix-refinement LLM would auto-correct — the motivation for local refinement.

## Next concrete step (before any implementation)

**Measure first.** Extend the throwaway bench tool (currently `C:\Users\hagis\dev\blurt-bench`,
outside the repo) with LLamaSharp + Vulkan and a Gemma E2B/E4B GGUF, and time the refine step on
the 78 s clip — CPU vs Vulkan. If latency is unacceptable there, it won't be acceptable embedded
either (Ollama and LLamaSharp share the `llama.cpp` engine). Decide go/no-go from that number,
then grill the design.

## Open questions

- Refinement latency on target-class hardware (the gating unknown).
- Portable size impact — the model is multi-GB and would be **downloaded separately** (like the
  whisper models today, see issue 22 flow), not bundled into the EXE.
- Keep Ollama + cloud as alternatives? Yes — all three coexist behind `IRefiner`; this only adds
  an option, it removes nothing.
