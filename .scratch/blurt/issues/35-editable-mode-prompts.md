# 35 — Editable prompts for every refined mode

Status: done (HITL UX check passed 2026-06-14)
Type: AFK feature

## Parent

[.scratch/blurt/PRD.md](../PRD.md)

## What to build

Every refined mode — Fix, English, Bullets, Custom — gets its own **editable
prompt**, surfaced as a field in the Settings window, pre-filled with our default
prompt for that mode. The user can override any of them; an untouched field keeps
the default behaviour. The refiner uses the configured prompt, read fresh per
dictation so an edit takes effect without a restart.

Today Fix/English/Bullets prompts are hard-wired in `RefinementPrompts` and only
Custom is configurable. This slice moves all of them into per-mode config (with the
current strings as the defaults), so they become the single editable source. Pur is
unaffected — it has no prompt (verbatim, zero-network) and must stay that way.

## Acceptance criteria

- [x] Settings shows one editable prompt field per refined mode (Fix, English,
      Bullets, Custom), each pre-filled with its default.
- [x] Editing a prompt and saving changes that mode's refinement on the next
      dictation, no restart.
- [x] An unedited install behaves exactly as today (defaults unchanged).
- [x] Pur remains promptless / zero-network.
- [x] The defaults + per-mode config live in `Blurt.Core`, unit-tested; suite green.

## Blocked by

- None — can start immediately.

## Comments

Built (2026-06-13, ralph/backlog loop):

- **Core (the editable source).** Added a `RefinedMode` enum (Fix/English/Bullets/Custom
  — Pur deliberately excluded, it stays verbatim/promptless) and a pure `ModePrompts`
  resolver: `DefaultFor(mode)` returns the shipped wording (the existing
  `RefinementPrompts` constants for the always-on modes, empty for Custom) and
  `For(mode, config)` returns the user's override or the default. Always-on modes
  fall back to the default when blanked (can't be silently disabled); Custom keeps its
  blank-means-no-refiner contract.
- **Config.** `BlurtConfig` gained `FixPrompt`/`EnglishPrompt`/`BulletsPrompt`, each
  defaulting to its `RefinementPrompts` constant, wired into `Equals`/`GetHashCode`.
  A config written before this setting has no keys for them, so it deserialises to the
  constant defaults → behaviour unchanged. (`CustomPrompt` was already configurable.)
- **Single source of truth.** `FlexSlotPrompts.For` now resolves Bullets/Custom through
  `ModePrompts`; `TrayApplicationContext` resolves the Fix/English prompts fresh per
  dictation via `_settings.Load()`, so a Settings edit applies on the next dictation
  with no restart.
- **Settings UI.** New "Mode prompts (LLM)" card with editable Fix/English/Bullets
  fields, pre-filled and persisted; Custom's field stays in the Flex-slot card.
- **Tests.** New `ModePromptsTests` (defaults, override, blank-handling, per-mode
  independence) + `SettingsStore` round-trip and a legacy-config backward-compat test.
  Full Core suite green (228 tests). Pur path untouched (`FlexSlotPromptsTests` still pass).

HITL UX check recommended: open Settings → "Mode prompts (LLM)", confirm Fix/English/Bullets
fields are pre-filled with the defaults; edit one (e.g. Fix), Save, dictate in that mode and
confirm the new prompt takes effect without restarting; clear a field, Save, and confirm the
mode reverts to its default behaviour.
