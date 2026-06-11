# 13 — Fail-soft error handling + Notifier

Status: ready-for-human
Type: AFK logic / HITL verification

## Parent

[.scratch/blurt/PRD.md](../PRD.md)

## What to build

A consistent fail-soft layer with a `Notifier` (tray + overlay messages) so no
failure ever crashes the app. Cover the four failure modes from the design:
no mic / permission denied → notice, no crash; transcription fails → notice,
nothing inserted; refinement endpoint unreachable → insert raw text + notice
("refinement offline, raw text inserted"); paste/injection blocked by the target
app → leave text on the clipboard + notice. Consolidate the ad-hoc fallbacks from
earlier slices behind one path.

## Acceptance criteria

- [ ] Tests (where seam allows): endpoint-unreachable inserts raw text + notice; injection-blocked leaves text on clipboard + notice.
- [ ] No-microphone / permission-denied produces a clear notice and the app keeps running.
- [ ] Transcription failure inserts nothing and notifies.
- [ ] Every notice is shown via the `Notifier` (tray + overlay), not a blocking dialog.

## Blocked by

- 09 — Refiner client (OpenAI-compatible) + Fix mode

## Implementation note (handoff)

Built a single fail-soft layer so no failure crashes the app, with every notice
routed through one `INotifier`.

What was built:

- **`InjectionBlocked` outcome** (`DictationOutcome`). `DictationPipeline.RunAsync`
  now evaluates the `bool` from `ITextInjector.InjectAsync`: `false` → the paste
  was refused, the text is left on the clipboard, and the pipeline returns
  `InjectionBlocked`. This dominates `RefinedOffline` (the user-visible problem is
  the text not landing). Covers design §10 mode 4.
- **`INotifier` seam + `NoticeLevel { Info, Warning, Error }`** in Core
  (`src/Blurt.Core/Notifier.cs`): `void Notify(string message, NoticeLevel level)` —
  must never block or throw. Concrete tray/overlay rendering lives in App.
- **Pure mapping `DictationNotices.For(DictationOutcome) → DictationNotice?`**
  (`record DictationNotice(string Message, NoticeLevel Level)`): `Injected` → null
  (silent); `NothingTranscribed` → Info "(no speech detected)"; `TranscriptionFailed`
  → Error; `RefinedOffline` → Warning; `InjectionBlocked` → Warning "Couldn't paste
  — text left on clipboard." Unit-tested per outcome.
- **`TrayNotifier : INotifier`** (`src/Blurt.App/TrayNotifier.cs`): the one place
  `ShowBalloonTip` is called, mapping `NoticeLevel` → `ToolTipIcon`. Comment marks
  the seam where the **overlay** channel plugs in (issue 06; tray-only today).
- **Consolidation** in `TrayApplicationContext`: the scattered per-outcome
  `switch`/`ShowBalloonTip` blocks in `DictateAsync`/`RefineAndInjectAsync` collapse
  to `Notify(outcome)` → `DictationNotices.For` → `_notifier`. Provisioning catch
  blocks, the download balloon, the flex-slot tap-cycle balloon, and the empty-custom
  hint all go through `_notifier` now too — no direct `ShowBalloonTip` left in the
  context.
- **Microphone fail-soft (mode 1):** `TryStartRecording()` wraps `_recorder.Start()`,
  catches NAudio device/permission exceptions, notifies ("No microphone available: …"),
  and returns `false`; all three Down handlers (English/Fix/Flex-slot) only enter
  recording on success. `AudioRecorder.Start()` now resets its state on a failed
  `StartRecording()` so `IsRecording` stays false and the next press starts clean.

Tests: 7 new (2 pipeline `InjectionBlocked`, 5 `DictationNotices` mapping). Full Core
suite green at 77 (was 70). `dotnet build src/Blurt.App` green (0 warnings/errors).
Pure Core logic is unit-tested; the App adapters are manual-verify only.

Manual checks (HITL) — hardware/target-app bound, not unit-testable:

- [ ] Disable/unplug the microphone, then press a dictation key → "No microphone
      available" notice, app keeps running (no crash).
- [ ] Point the refiner at an offline/unreachable endpoint, dictate a Fix/English
      utterance → raw transcript inserted + "Refinement offline — raw text inserted."
      notice.
- [ ] Dictate into an app that blocks paste (or where Ctrl+V is swallowed) → text
      stays on the clipboard (paste it manually) + "Couldn't paste — text left on
      clipboard." notice.
- [ ] Trigger a transcription failure → nothing inserted + "Transcription failed."
      notice.
