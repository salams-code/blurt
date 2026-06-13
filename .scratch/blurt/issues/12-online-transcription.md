# 12 — Online transcription option (OpenAI Whisper API)

Status: done (HITL-verified 2026-06-13)
Type: AFK

## Parent

[.scratch/blurt/PRD.md](../PRD.md)

## What to build

An `OpenAiWhisper` implementation of `ITranscriber` that sends audio to the OpenAI
Whisper API, selectable via a one-click switch in settings as an alternative to
the local model. Useful when local latency is unacceptable; it trades off the
offline guarantee (so it is not available for the offline promise of Pur — Pur
stays local-only). The pipeline picks the transcriber based on the configured
setting.

## Acceptance criteria

- [x] Test: with "online" selected, the pipeline uses `OpenAiWhisper` (asserted against a mock endpoint); with "local", it uses `LocalWhisper`.
- [x] Switching the transcription source in settings takes effect without restart (or with a clearly defined restart).
- [x] Pur's zero-network guarantee is documented/enforced as local-only.

## Comments

**2026-06-12 (agent):** Built TDD:

- **Core `OpenAiWhisper : ITranscriber`** — multipart POST (file + model
  `whisper-1`) to `{base}/audio/transcriptions`, Bearer key (none sent when
  empty), throws on non-success so the pipeline fail-softs to
  `TranscriptionFailed` — the same contract as `LocalWhisper`. Injected
  `HttpClient`; 4 unit tests incl. the end-to-end mock-endpoint pipeline test.
- **Core `TranscriberResolver.ResolveAsync(mode, zeroNetwork, local, online)`** —
  factories, so with Online selected the local model is never provisioned (and
  vice versa). `zeroNetwork: true` forces local regardless of mode: **Pur's
  offline promise is enforced by the resolver, not by call-site discipline**
  (unit-tested). 3 tests.
- **App wiring:** both dictation paths resolve per dictation
  (`ResolveTranscriberAsync` in `TrayApplicationContext`): the verbatim path
  (Pur / raw fallback) passes `zeroNetwork: true`; the refined modes follow the
  configured source. Config + DPAPI key are read fresh each time → **source
  switch applies from the next dictation, no restart**. Online always targets
  `https://api.openai.com/v1` (transcription is not per-provider configurable —
  the online option exists for latency, not arbitrary endpoints).
- Settings hint now says: source applies from the next dictation, Pur stays
  local/offline, only the local-model change needs a restart.

Known UX edge (fail-soft, by design): Online selected with no stored API key →
401 → "transcription failed" notice. A friendlier proactive notice is a
possible follow-up.

Suite green (183/183). HITL: real-key smoke test (corporate machine is
proxy-blocked; needs the home machine).

## Blocked by

- 04 — Audio capture + local Whisper transcription
- 08 — SettingsStore: JSON config + DPAPI-encrypted API key
