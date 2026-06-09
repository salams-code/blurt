# 05 — Pur dictation end-to-end

Status: ready-for-human
Type: HITL

## Parent

[.scratch/blurt/PRD.md](../PRD.md)

## What to build

The core tracer bullet: wire the three primitives into one push-to-talk flow.
Hold the hotkey → record (02 hook + 04 recorder) → transcribe locally (04) →
inject verbatim at the cursor (03). This is "Pur" mode — verbatim Whisper output,
no LLM call, zero network. Introduce a minimal pipeline that owns the
record → transcribe → inject sequence so later modes can hook in.

## Acceptance criteria

- [ ] Holding the hotkey, speaking, and releasing inserts the transcribed text at the cursor.
- [ ] Pur mode makes zero network calls.
- [ ] The original clipboard is preserved across the operation.
- [ ] The end-to-end flow works in at least two target apps.
- [ ] The orchestration is structured so a refinement step can be inserted later without rewriting it.

## Blocked by

- 02 — Keyboard hook fires and swallows the AltGr trigger character
- 03 — Text injection at the cursor via clipboard
- 04 — Audio capture + local Whisper transcription
