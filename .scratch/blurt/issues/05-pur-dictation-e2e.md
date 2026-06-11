# 05 — Pur dictation end-to-end

Status: done
Type: HITL

**2026-06-11 (user):** Live-Check bestanden — verbatim Injection in mehreren
Apps, Original-Clipboard erhalten, und nach dem Fix fügt Stille nichts mehr ein
(Whisper-Marker `[BLANK_AUDIO]`/`(Musik)` werden als „keine Sprache" behandelt).
Issue geschlossen.

## Implementation note (handoff)

**2026-06-11 (live check follow-up):** Manual checks 1–3 passed (verbatim
injection in several apps, original clipboard preserved). Check 5 surfaced a
gap: Whisper does not return an empty string for silence/noise — it emits a
bracketed annotation (`[BLANK_AUDIO]`, `(Musik)`, `[MUSIC]`…), which the
`IsNullOrWhiteSpace` guard let through, so the marker was injected at the cursor.
Fixed in `DictationPipeline`: a transcript that is *entirely* bracketed/
parenthesised annotation (plus whitespace) now counts as `NothingTranscribed`
and injects nothing; genuine speech containing a parenthetical keeps real words
outside the brackets and is injected **verbatim** (annotations never stripped
from real dictation). 2 new Core tests (non-speech markers via Theory;
parenthetical-preservation guard) — 42 Core tests total, all green.

The record → transcribe → inject sequence is now owned by a headless Core
pipeline, `DictationPipeline` (`Blurt.Core`). It depends on two seams:
`ITranscriber` (existing) and a new `ITextInjector`
(`Task<bool> InjectAsync(string text, CancellationToken ct = default)`) which
the concrete `TextInjector` now implements (its `InjectAsync` gained an optional
`ct`; existing callers/tests unchanged). `RunAsync(Stream wavAudio, ct)`
transcribes, runs an optional refinement step, then injects — returning a
`DictationOutcome` enum (`Injected` / `NothingTranscribed` /
`TranscriptionFailed`) so the caller can show the right notice. Fail-soft:
empty/whitespace transcript → no injection; transcription throws → caught, no
injection, outcome signals it.

**Pur = zero network**, structurally: the refinement step is an optional
delegate `Func<string, CancellationToken, Task<string>>? refine` (null in Pur,
so nothing touches the verbatim text). A later mode (issue 09) inserts an LLM
rewrite by constructing the pipeline with a non-null transform — no rewrite of
`RunAsync`. A unit test (`A_refinement_step_runs_between_transcription_and_injection`)
proves the seam works.

6 new Core tests (28 total, all green): tracer (transcribe + inject), empty/
whitespace ×3 (Theory, no injection), transcription error (fail-soft), refinement
seam. xUnit + hand-rolled fakes, behaviour over public API — same style as
`TextInjectorTests`.

Wiring in `TrayApplicationContext` (English trigger `AltGr + .`): the old
balloon path (`TranscribeAndShowAsync`) is replaced by `DictateAsync`, which
builds a `DictationPipeline` over the lazily-provisioned `LocalWhisper` (wrapped
in a private `OffloadedTranscriber` that pushes the CPU decode to the thread
pool) and the existing `_textInjector`, then runs it. Success injects silently;
`NothingTranscribed` → "(no speech detected)" balloon; `TranscriptionFailed` /
provisioning failure → error balloon. Recorder/`AsyncLazy` provisioning, Fix and
FlexSlot triggers untouched. `build src/Blurt.App` green.

Remaining = the manual, hardware/OS-bound checks (run from the native Windows
folder; model already at `%APPDATA%\Blurt\models\`, do **not** re-download):
1. Run `Blurt.exe`. Focus Notepad.
2. Hold `AltGr + .`, speak a German sentence, release → within ~2–4 s the
   transcribed German text appears **at the cursor** in Notepad (verbatim, no
   balloon on success). Empty/silent recording → "(no speech detected)" balloon.
3. Repeat in a second app (e.g. a browser address bar or text field) → text
   lands at the cursor there too (≥2 target apps).
4. Before dictating, put something distinctive on the clipboard; after the
   injection, paste (Ctrl+V) elsewhere → the **original clipboard is intact**.
5. Offline confirmation: Pur makes zero network calls (no refiner in the path).

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
