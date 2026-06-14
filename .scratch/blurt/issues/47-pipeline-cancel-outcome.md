# 47 â€” Pipeline cancellation: Cancelled outcome, distinguished from failures

Status: done (2026-06-14 — shipped in PR #4)

## Parent

New feature request (overlay cancel button â€” this session). Foundation slice for issue 48.

## What to build

Make cancelling a dictation a first-class, clean outcome rather than a masked failure.

`DictationPipeline.RunAsync` already threads a `CancellationToken` into the transcriber,
refiner and injector â€” but its `catch` blocks currently swallow **everything**
([DictationPipeline.cs:92,143](../../../src/Blurt.Core/DictationPipeline.cs#L92)), so a cancel
would be mis-reported as `TranscriptionFailed` / `RefinedOffline`. Add a
`DictationOutcome.Cancelled` and treat an `OperationCanceledException` as deliberate
cancellation: no text injected, outcome `Cancelled`. The caller maps `Cancelled` to a clean
return-to-Idle with no error notice.

Crucially, real transcription/refiner failures must STILL map to their existing outcomes â€”
cancellation must not become a catch-all that masks genuine errors.

## Acceptance criteria

- [ ] `DictationOutcome.Cancelled` exists.
- [ ] A transcriber throwing `OperationCanceledException` â†’ `RunAsync` returns `Cancelled`; the injector is never called. Unit-tested (RED first).
- [ ] A refiner throwing `OperationCanceledException` â†’ `Cancelled` (not `RefinedOffline`); nothing injected. Unit-tested.
- [ ] Genuine transcription/refiner failures still map to `TranscriptionFailed` / `RefinedOffline` (cancellation must not mask real errors). Unit-tested.
- [ ] The `Notifier` maps `Cancelled` to no error notice (silent, or a brief neutral "abgebrochen").
- [ ] Suite stays green.

## Blocked by

None â€” can start immediately.
