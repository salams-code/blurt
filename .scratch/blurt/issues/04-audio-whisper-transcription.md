# 04 — Audio capture + local Whisper transcription (visible result)

Status: ready-for-human
Type: HITL

## Parent

[.scratch/blurt/PRD.md](../PRD.md)

## What to build

An `AudioRecorder` (NAudio) that captures microphone audio while a key is held,
and a `LocalWhisper` implementation of `ITranscriber` (Whisper.net / whisper.cpp)
that turns that audio into raw text. Use a multilingual model (not `.en`),
default `small` (q5), downloaded to `%APPDATA%\Blurt\models\` on first run.
CPU-bound; GPU acceleration auto-enabled only if usable hardware is detected.
For this slice the result is just surfaced visibly (tray balloon, console, or a
debug window) — no injection yet.

## Acceptance criteria

- [ ] Holding the key records audio and releasing stops it.
- [ ] German speech is transcribed to reasonably accurate German text via the local model.
- [ ] The `small` model is downloaded on first run if missing, into the models folder.
- [ ] Transcription latency for a short utterance is in the expected ballpark (~2–4 s on CPU with `small`).
- [ ] `ITranscriber` is an interface so it can be faked/replaced later.

## Blocked by

- 01 — Solution skeleton + tray that runs
