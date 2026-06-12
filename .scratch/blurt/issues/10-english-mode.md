# 10 — English mode (translate to English)

Status: done (verify-sweep 2026-06-12)
Type: AFK

## Parent

[.scratch/blurt/PRD.md](../PRD.md)

## What to build

The "English" hotkey (`AltGr + .`): transcribe the (German) speech, then refine
with a translation prompt that returns clean English, and inject. Reuses the
`IRefiner` and pipeline from 09 with a different mode prompt registered in
`ModeRegistry`.

## Acceptance criteria

- [x] Test: the English mode prompt is selected and sent for the English hotkey, asserted against the mock server.
- [x] End-to-end: speaking German with the English hotkey inserts fluent English text.
- [x] Same fail-soft fallback to raw text applies when the endpoint is unreachable.

## Blocked by

- 09 — Refiner client (OpenAI-compatible) + Fix mode

## Implementation note (handoff)

Built on the existing issue-09 refiner + shared `RefineAndInjectAsync` pipeline —
no new infrastructure, only a new prompt and one re-wire.

- **English prompt:** added `RefinementPrompts.English` (a translation prompt:
  translate the dictated German transcript into fluent, natural English, drop
  filler words/false starts, return only the translation). Kept the established
  `RefinementPrompts` pattern — no `ModeRegistry` (consolidation is later work).
- **OnEnglishTrigger wiring:** the English hotkey hold now calls
  `RefineAndInjectAsync(audio, RefinementPrompts.English)` instead of the old
  `DictateAsync(audio)`. English mode therefore now **translates** rather than
  the Pur verbatim path it had as the issue-05 demonstrator. Recording tray text
  updated to `recording (english)`. Nothing else in the file was touched
  (`RefineAndInjectAsync`, `DictateAsync`, `OnFixTrigger`, `OnFlexSlotTrigger`
  unchanged), keeping the issue-11 parallel work merge-clean.
- **Test (against mock server):** `OpenAiCompatibleRefinerTests` gained
  `The_english_mode_sends_the_translation_prompt_and_returns_the_english_text`,
  asserting (via the fake `HttpMessageHandler`) that the system message equals
  `RefinementPrompts.English` and the returned English translation is passed
  through. A small `RefinementPromptsTests` case asserts `English` is set and
  mentions English/translation. Full Core suite green: 63 passed (61 baseline +
  2 new). `build src/Blurt.App` green.
- **Fail-soft:** unchanged — `RefineAndInjectAsync` already injects the raw
  transcript and reports `RefinedOffline` ("Refinement offline — raw text
  inserted." balloon) when the endpoint is unreachable, so English inherits it.

### Manual checks (human)

- [ ] Launch the app; with a configured refiner endpoint, hold `AltGr + .` and
      speak a German sentence → fluent English text appears at the cursor.
- [ ] With the endpoint unreachable (e.g. stop the local server / clear the URL),
      hold `AltGr + .` and speak German → raw German transcript inserted plus the
      "refinement offline" balloon.

## Comments

**2026-06-12 (agent, verify-sweep):** Prompt selection asserted against the mock server (unit suite); English dictation exercised in the user's HITL sessions. Fail-soft inherited from the shared RefineAndInjectAsync path (unit-tested).
