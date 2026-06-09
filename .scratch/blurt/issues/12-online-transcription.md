# 12 — Online transcription option (OpenAI Whisper API)

Status: ready-for-agent
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

- [ ] Test: with "online" selected, the pipeline uses `OpenAiWhisper` (asserted against a mock endpoint); with "local", it uses `LocalWhisper`.
- [ ] Switching the transcription source in settings takes effect without restart (or with a clearly defined restart).
- [ ] Pur's zero-network guarantee is documented/enforced as local-only.

## Blocked by

- 04 — Audio capture + local Whisper transcription
- 08 — SettingsStore: JSON config + DPAPI-encrypted API key
