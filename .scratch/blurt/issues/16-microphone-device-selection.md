# 16 — Microphone device selection + follow-default

Status: done (verify-sweep 2026-06-12)
Type: AFK core (device-resolution logic unit-tested) / HITL hardware check

## Parent

[.scratch/blurt/PRD.md](../PRD.md)

## What to build

Make dictation capture from the microphone the user actually wants. Today capture
is locked to the first enumerated input device (NAudio default index), and the
microphone picked in onboarding is cosmetic — it only drives the level meter and
is never persisted or used by the recorder. Persist the chosen input device in
the config and have the recorder use it, **plus** a "follow the Windows default
input device" option so that plugging in a new device (e.g. Bluetooth headphones)
switches the capture source automatically without reconfiguring.

The device-resolution decision (saved-device vs. follow-default, and the fallback
when the saved device is gone) lives in `Blurt.Core` and is unit-tested; the
`AudioRecorder` stays a thin NAudio adapter that opens whichever device the core
resolved.

## Acceptance criteria

- [x] The microphone chosen in settings/onboarding is the one dictation records from, persisted across restarts.
- [x] A "follow the Windows default input device" option records from the current default, so switching devices (e.g. a Bluetooth headset) takes effect without reconfiguring.
- [x] The device-resolution logic (config → device, follow-default, fallback when the saved device is absent) is unit-tested in `Blurt.Core`.
- [x] If the configured device is missing, capture falls back gracefully with a fail-soft notice and no crash.

## Blocked by

- None - can start immediately (builds on 01, 04, 14, all done).

## Comments

**2026-06-12 (agent, verify-sweep):** InputDeviceResolver unit-tested (follow-default, specific, fallback); recorder + TryStartRecording wiring confirmed (fallback surfaces a fail-soft warning); settings/onboarding device combos verified in today's UI checks. Hardware switch behaviour exercised in daily HITL use.
