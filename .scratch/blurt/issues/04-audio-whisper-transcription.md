# 04 — Audio capture + local Whisper transcription (visible result)

Status: ready-for-human (implemented; live mic+model check pending)
Type: HITL

## Implementation note (handoff)

Headless model-provisioning logic lives in `Blurt.Core` (`WhisperModel`,
`ModelProvisioner`, `IModelDownloader` seam; 4 unit tests: path resolution,
download-when-missing, skip-when-present, presence query) plus the
`ITranscriber` interface (16 kHz 16-bit mono PCM WAV contract). Thin adapters
in `Blurt.App`: `AudioRecorder` (NAudio `WaveInEvent`, in-memory WAV),
`GgmlModelDownloader` (Whisper.net's Hugging Face downloader, temp-file +
move so half-downloads never count as present), `LocalWhisper`
(`WhisperFactory.FromPath` + language auto-detect, segments joined). Wired in
`TrayApplicationContext` for the **English trigger only** (`AltGr + .`):
down = record, up = transcribe on a background task, transcript in a tray
balloon. Fix/FlexSlot handling untouched. Model: multilingual
`ggml-small-q5_1.bin`, fetched lazily on the first dictation — never during
build/tests.

Remaining = the manual check below, to run from the native Windows folder:
1. Run `Blurt.exe` (first run: hold `AltGr + .` briefly once — a balloon says
   "Downloading Whisper model (ggml-small-q5_1.bin, ~460 MB)…"; wait for it to
   land in `%APPDATA%\Blurt\models\`).
2. Hold `AltGr + .` and speak a German sentence; release.
3. Within ~2–4 s a balloon "Blurt transcript" shows reasonably accurate German
   text. Empty/silent recordings show "(no speech detected)".
4. Subsequent runs: no download balloon (model already present).

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

**2026-06-11 (agent):** Live check on the corporate laptop: the Hugging Face
model download fails ("SSL connection could not be established" — corporate
proxy/TLS interception). Two consequences handled:
1. Bug fixed: the failed provisioning task was cached forever (`??=`), so every
   later dictation refailed instantly until app restart. Provisioning now uses
   `AsyncLazy<T>` (Core, unit-tested), which forgets failed attempts.
2. Workaround for blocked networks: download `ggml-small-q5_1.bin` manually
   (huggingface.co/ggerganov/whisper.cpp) into `%APPDATA%\Blurt\models\` —
   provisioning then skips the download. A proper alternative source/setting
   belongs to issue 12 (online transcription) / 14 (settings window).
