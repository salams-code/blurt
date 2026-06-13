# 30 — Online transcription has no offline → local fallback (Full Cloud breaks with no internet)

Status: triage (reported by user 2026-06-13, confirmed against code)

## Parent

[.scratch/blurt/PRD.md](../PRD.md)

## What the user observed (2026-06-13)

On **Full Cloud** with a brief internet outage, a dictation **failed outright** —
it did **not** fall back to local transcription, and the configured large model's
download also failed (no network). The dictation was lost (TranscriptionFailed
notice).

## Confirmed cause (code, not a regression — a missing feature)

- `TranscriberResolver.ResolveAsync` ([Transcription.cs](../../../src/Blurt.Core/Transcription.cs)):
  `zeroNetwork || mode == Local ? local() : online()`. For a refined mode on
  **Online** source it returns the OpenAI transcriber with **no local fallback**.
- `DictationPipeline.RunAsync` ([DictationPipeline.cs](../../../src/Blurt.Core/DictationPipeline.cs)):
  a transcription throw is caught → `TranscriptionFailed`, nothing injected. Only
  **refinement** has an offline fallback (`RefinedOffline` → inject raw transcript);
  **transcription has none**.
- Compounding: the user's configured local model is **large-v3-turbo**, which is
  **not on disk** (only `ggml-small-q5_1.bin` is). So even a local fallback would
  have triggered a ~1.5 GB download that can't complete offline — unless it fell
  back to the already-present model.

So Full Cloud + offline = dead dictation, by current design.

## Design questions for triage (don't over-build)

- Should **Online transcription fall back to local whisper.cpp** when the network
  call fails (mirroring the refinement `RefinedOffline` fail-soft)? That makes the
  cloud tiers degrade gracefully offline.
- If so, **which local model** does the fallback use — the configured one (may be
  absent → can't download offline), or **any already-present model** (e.g. the
  small one on disk)? Falling back to "whatever is installed" avoids the offline
  download trap.
- Should the fallback be **silent** or surface a "transcribed locally (offline)"
  notice, like `RefinedOffline`?
- Separate but related: the configured model (large-v3-turbo) not being installed
  is its own gap — Pur/local already silently can't work until it's fetched. Worth
  a clearer "selected model not installed" signal (see issue 22 territory).

## Acceptance criteria (draft — refine in triage)

- [ ] A network failure during Online transcription falls back to a usable local
      model rather than losing the dictation (decision: which model).
- [ ] The fallback is fail-soft and surfaced with an appropriate notice.
- [ ] Pure decision lives in `Blurt.Core` (extend `TranscriberResolver` /
      pipeline), unit-tested; the suite stays green.

## Notes

Reported live; not yet reproduced under instrumentation. The "fresh-machine /
real-dictation" test (see HANDOFF) is the place to reproduce and validate any fix.
