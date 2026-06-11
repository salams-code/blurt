# 20 — Hotkey capture in settings + suspend the global hook while configuring

Status: proposed — awaiting triage (found in manual test of the current build, 2026-06-12)
Type: AFK logic (capture/validation unit-tested) / HITL UI check

## Parent

[.scratch/blurt/PRD.md](../PRD.md)

## What to build

Make the hotkey fields in settings actually usable. Today they are free-text
boxes that expect the user to type the chord string (e.g. `AltGr+,`) by hand,
and there is no "press the keys to capture it" affordance. Worse: the global
low-level keyboard hook stays active while the settings window is open, so when
the user presses a real trigger chord to set it, the hook **swallows** the
character *and* fires the dictation trigger behind the window (recording starts
unseen). The net effect the user sees is "I can't type the hotkey in, and
pressing the combo does nothing."

Two parts of one slice:

1. **Press-to-capture control:** clicking a hotkey field and pressing the chord
   captures it (renders as `AltGr+,` and stores the same chord string the
   resolver already understands). Non-trigger keys are rejected with the existing
   guidance (only `, . -` after AltGr are valid). The capture state machine and
   "is this a valid trigger chord" decision live in `Blurt.Core` (building on the
   existing `HotkeyBinding`) and are unit-tested.
2. **Suspend the hook while configuring:** while a modal config surface (settings
   or onboarding) is focused, the global trigger hook must not fire dictation —
   so capturing a chord, or just having the window open, never starts a recording
   behind it. Thin App concern; restore the hook when the window closes.

## Acceptance criteria

- [ ] A hotkey field can be set by focusing it and pressing the chord; the captured chord persists and drives the trigger after save.
- [ ] Invalid (non-trigger) chords are rejected in-place with clear guidance; valid chords (`AltGr + , . -`) are accepted.
- [ ] While settings/onboarding is open and focused, pressing a trigger chord does not start a dictation behind the window.
- [ ] Chord capture/validation logic is unit-tested in `Blurt.Core`.
- [ ] No regression to remapping: a captured chord still re-installs the hook on save (issue 14 behaviour).

## Blocked by

- None functionally, but it edits `SettingsWindow` / `OnboardingWindow` / the
  hook wiring that issues 16–18 also touch — best sequenced **after** 16–18 land
  (like 19) to avoid restyling/re-merging the same controls twice.
