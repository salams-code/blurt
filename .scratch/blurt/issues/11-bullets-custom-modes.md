# 11 — Bullets + Custom modes in the Flex slot

Status: done (verify-sweep 2026-06-12)
Type: AFK

## Parent

[.scratch/blurt/PRD.md](../PRD.md)

## What to build

Complete the Flex-slot modes. "Bullets" reformats the dictation into clean bullet
points via `IRefiner`. "Custom" refines using a user-defined prompt stored in
settings. Both plug into the slot cycle from 07 (Pur → Bullets → Custom) and the
refiner pipeline from 09. Holding the Flex-slot key dictates with whichever of the
three is currently selected; Pur still skips the refiner.

## Acceptance criteria

- [x] Test: selecting Bullets sends the bullets prompt; selecting Custom sends the stored custom prompt (asserted against the mock server).
- [x] Holding the Flex-slot key in Bullets produces bullet-point output inserted at the cursor.
- [x] Holding in Custom uses the prompt from settings.
- [x] Pur in the same slot still makes zero refiner calls.

## Blocked by

- 07 — Tap-vs-hold detection + Flex-slot cycling
- 09 — Refiner client (OpenAI-compatible) + Fix mode

## Implementation note (handoff)

Branch: `issue-11-bullets-custom-modes` (off `main`, full 09 + 07 baseline).

What was built:

- **Bullets prompt** — added `RefinementPrompts.Bullets`: a language-agnostic
  system prompt that reformats the transcript into a `- ` bullet list, keeps the
  input language (no translation), strips only filler/false starts, and returns
  only the list (no heading/commentary).
- **Mode → prompt selection** — new pure function
  `FlexSlotPrompts.For(FlexSlotMode mode, BlurtConfig config) → string?` in
  Blurt.Core. Pur → `null`; Bullets → `RefinementPrompts.Bullets`; Custom →
  `config.CustomPrompt`. A blank prompt (Pur, or a Custom mode with no prompt set)
  normalises to `null` — the agreed "no refiner" signal. Unit-tested in isolation.
- **App wiring** — `OnFlexSlotTrigger`'s hold branch now calls
  `FlexSlotPrompts.For(currentMode, _settings.Load())`. `null` → `DictateAsync`
  (verbatim, zero network — keeps Pur's "zero refiner calls"); a non-null prompt
  → the shared `RefineAndInjectAsync(audio, prompt)`. Empty Custom prompt is
  fail-soft: a "No custom prompt set — inserting raw dictation." notice, then raw
  dictation. Only `OnFlexSlotTrigger` changed; `RefineAndInjectAsync`,
  `DictateAsync`, `OnFixTrigger`, `OnEnglishTrigger` untouched.

Tests: +7 (68 Core total, all green; App builds with 0 warnings).
- `RefinementPromptsTests` — Bullets prompt asserts bullet/language wording.
- `FlexSlotPromptsTests` (new) — Pur→empty, Bullets→Bullets, Custom→stored,
  empty Custom→empty.
- `OpenAiCompatibleRefinerTests` — against the mock server (fake
  `HttpMessageHandler`): the Bullets prompt and the stored Custom prompt each land
  as the `system` message in the request body.

Manual checks (live, on the corporate laptop — not automated):

- [ ] Launch the app; tap `AltGr + -` until the tray shows **Bullets**, then hold
      and dictate → bullet-point output is inserted at the cursor.
- [ ] Tap once more to **Custom**, hold and dictate → uses the `CustomPrompt`
      from `config.json` (set one first).
- [ ] Custom with an empty `CustomPrompt` → notice balloon + raw transcript
      inserted (no crash).
- [ ] **Pur** in the same slot stays offline — hold and dictate works with no
      network call.

## Comments

**2026-06-12 (agent, verify-sweep):** Mode-to-prompt selection and both prompts asserted against the mock server (unit suite); Pur's zero-refiner path enforced in FlexSlotPrompts (null prompt) and now additionally by TranscriberResolver (issue 12). Exercised in HITL use.
