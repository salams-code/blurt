# 30 — Online transcription has no offline → local fallback (Full Cloud breaks with no internet)

Status: done (triaged + fixed via TDD, 2026-06-13) — HITL offline repro still pending

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

- [x] A network failure during Online transcription falls back to a usable local
      model rather than losing the dictation (decision: any installed model).
- [x] The fallback is fail-soft and surfaced with an appropriate notice.
- [x] Pure decision lives in `Blurt.Core` (extended `DictationPipeline` +
      `ModelProvisioner`), unit-tested; the suite stays green (199).

## Triage decisions (user, 2026-06-13)

1. **Fall back?** Yes — fail-soft, mirroring the refinement `RefinedOffline`.
2. **Which model?** Any already-installed model (prefer the configured one if
   present, else any `ggml-*.bin` on disk). Never a download — that's what avoids
   the offline-download trap.
3. **Surface it?** Yes — its own notice, like `RefinedOffline`.

On "why did the download fail anyway": the user is on **Online** source, so the
`local` factory was never invoked and `ProvisionTranscriberAsync` (the only thing
that downloads) never ran — the configured `large-v3-turbo` was never fetched
(only `small` is on disk). When the network dropped, falling back to the
*configured* model would have tried a ~1.5 GB download that can't complete offline.
Falling back to *any installed* model (small) needs no network → the chosen design.

## What was built (TDD, suite 191→199)

- **Core `DictationOutcome.TranscribedOffline`** ([DictationPipeline.cs](../../../src/Blurt.Core/DictationPipeline.cs))
  — new fail-soft outcome, sibling of `RefinedOffline` for the transcription step.
- **`DictationPipeline`** gained an optional `transcribeFallback` delegate: when
  the primary transcriber throws and a fallback is wired, it rewinds the (seekable)
  WAV stream and transcribes through the fallback, then flows the result through
  refinement as normal. Precedence: `InjectionBlocked` > `TranscribedOffline` >
  `RefinedOffline` > `Injected`. 5 new pipeline tests (incl. the both-offline case
  and the stream-rewind).
- **`ModelProvisioner.FindInstalledModelPath(preferred)`** ([ModelProvisioning.cs](../../../src/Blurt.Core/ModelProvisioning.cs))
  — returns the configured model if present, else any installed `ggml-*.bin`, else
  null (never downloads). 3 new tests.
- **Notice** ([Notifier.cs](../../../src/Blurt.Core/Notifier.cs)): "Cloud
  transcription offline — transcribed locally." (Warning).
- **App wiring** ([TrayApplicationContext.cs](../../../src/Blurt.App/TrayApplicationContext.cs)):
  `BuildLocalFallback()` builds the delegate from any installed model (offloaded
  off the UI thread; null when nothing is installed → stays `TranscriptionFailed`),
  wired into the refined-dictation pipeline.

Out of scope (separate gap, issue 22 territory): a clearer "selected model not
installed" signal so an Online user knows their configured local model was never
fetched.

## Notes

Reported live; not yet reproduced under instrumentation. The "fresh-machine /
real-dictation" test (see HANDOFF) is the place to reproduce and validate the fix
end-to-end (Full Cloud, kill the network mid-dictation, confirm local fallback +
the new notice).
