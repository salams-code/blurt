# 24 — Settings provider switch should present per-provider endpoint fields (parity with onboarding)

Status: proposed — awaiting triage (found in HITL test, 2026-06-12)
Type: AFK Core (per-provider endpoint config) + App UI / HITL UX check

## Parent

[.scratch/blurt/PRD.md](../PRD.md)

## What's wrong (HITL finding, 2026-06-12)

In **Settings**, switching the refinement Provider (OpenAI ↔ local/Ollama) only
updates the *hint* line — the **Base URL and Model text fields keep the OpenAI
values**. So after switching to Ollama the user still sees
`https://api.openai.com/v1` sitting in the Base URL field and must clear/retype it
by hand.

**Onboarding** already does the right thing: switching provider there presents the
provider-appropriate links/values, so the two providers feel like two separate
paths. Settings is inconsistent with that.

The user wants the two providers treated as **two separate paths** in Settings
too: switching to Ollama should show Ollama-appropriate endpoint values (e.g.
`http://localhost:11434/v1`, empty key), and switching back to OpenAI should show
the OpenAI values again — ideally **remembering each provider's own base URL +
model** rather than overwriting a single shared field.

## Design questions for triage

- **Remember per provider** (preferred by user): store base URL + model **per
  provider** in the config, swap the field contents on switch, persist both. A
  schema change in `Blurt.Core` (the config currently holds a single
  `RefinementBaseUrl` / `RefinementModel`). Migration: seed the OpenAI slot from
  the existing values.
- vs. **fill defaults on switch**: just overwrite the fields with that provider's
  default endpoint when the provider changes (simpler, but loses a custom URL when
  toggling back and forth).

The provider→endpoint resolution belongs in `Blurt.Core` and is unit-tested; the
settings UI is the thin shell. Keep onboarding and settings consistent.

## Acceptance criteria (draft — refine in triage)

- [ ] Switching provider in Settings updates the Base URL and Model fields to that provider's values (not just the hint).
- [ ] Switching back restores the other provider's values (per-provider memory), without deleting the stored API key.
- [ ] Onboarding and Settings present the provider choice consistently.
- [ ] Per-provider endpoint resolution is unit-tested in `Blurt.Core`; the suite stays green; the app builds.

## Blocked by

- Best verified after issue 23 (Settings Save/Cancel crash) so a switch can be
  saved and reloaded. Builds on issue 17 (switchable provider).
