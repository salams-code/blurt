# 21 — Flex-slot tap must not block the UI thread

Status: done (HITL-verified 2026-06-13)
Type: AFK (UI-thread/latency fix) / HITL feel check

## Parent

[.scratch/blurt/PRD.md](../PRD.md)

## What to build

Make a quick Flex-slot tap (cycle the mode) feel instant and reliable. By design
recording starts on every key-down (you can't know tap-vs-hold until release),
and a tap (< 250 ms) discards the take and cycles the mode. The problem: on the
tap path, `_recorder.Stop()` runs **synchronously on the UI thread** and blocks
up to ~2 s waiting for NAudio's `RecordingStopped` to drain the just-started
device (see [AudioRecorder.cs](../../../src/Blurt.App/AudioRecorder.cs) and the
tap branch in
[TrayApplicationContext.cs](../../../src/Blurt.App/TrayApplicationContext.cs)).
Starting and immediately stopping `WaveInEvent` inside the tap window is also
race-prone. The user sees the overlay jump to "listening" and then hang for a
moment before the mode cycles — "not reliable."

Fix the tap path so it never blocks the UI thread: stop/dispose the discarded
recording off-thread (or give `AudioRecorder` a non-blocking discard), so the
mode cycles immediately while the device tears down in the background. Keep the
pure 250 ms tap/hold classifier in `Blurt.Core` (already unit-tested); add Core
coverage if a new decision is introduced (e.g. a minimum-viable-take guard).

## Acceptance criteria

- [x] A quick tap cycles the mode immediately (no multi-second wait, no stuck "listening" pill).
- [x] The UI thread is never blocked on the tap (discard) path.
- [x] A hold still records and transcribes correctly; rapid tap→hold→tap sequences stay reliable. *(HITL feel check)*
- [x] Any new timing/guard decision is unit-tested in `Blurt.Core`; the suite stays green. *(no new Core decision — the 250 ms classifier is unchanged)*

## Comments

**2026-06-12 (agent) — triage + fix:** Confirmed the diagnosis: the tap branch
(and the stale-key-up abort) called `_recorder.Stop().Dispose()` on the UI
thread; `Stop` waits up to 2 s on NAudio's `RecordingStopped`. Fix:
`AudioRecorder.Discard()` — non-blocking, no-op when idle:

1. unsubscribes `DataAvailable` **synchronously first**, so a late device buffer
   can never land in the *next* recording's writer during a rapid tap→hold
   (that race existed before the fix, too);
2. detaches all recorder state immediately (`IsRecording` false, next press
   starts fresh);
3. stops/disposes the device + writer + buffer on the thread pool, swallowing
   teardown errors (there is nothing to save on the discard path).

Both tap-path call sites in `TrayApplicationContext` now use `Discard()`;
`Stop()` (hold → transcription) and `Dispose()` (app exit) are unchanged. App
builds, suite green (173/173). The "feels instant" check is hardware-bound →
HITL.

## Blocked by

- None. Independent of 16–19 (touches the recorder + flex-slot dispatch, not the
  settings/onboarding UI).
