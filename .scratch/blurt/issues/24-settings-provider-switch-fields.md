# 24 — Settings provider switch should present per-provider endpoint fields (parity with onboarding)

Status: ready-for-human (implemented 2026-06-12, per-provider memory; awaiting HITL UX check)
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

- [x] Switching provider in Settings updates the Base URL and Model fields to that provider's values (not just the hint).
- [x] Switching back restores the other provider's values (per-provider memory), without deleting the stored API key.
- [x] Onboarding and Settings present the provider choice consistently.
- [x] Per-provider endpoint resolution is unit-tested in `Blurt.Core`; the suite stays green; the app builds.

## Comments

**2026-06-12 (agent) — triage + build:** Took the user-preferred **remember per
provider** design (TDD):

- Core `ProviderEndpoints` — `DefaultFor(provider)` (OpenAI: api.openai.com/v1 +
  gpt-4o-mini; Local: localhost:11434/v1 + llama3.1) and pure
  `Switch(from, current, to, remembered)` → (target endpoint, updated map).
  3 unit tests: defaults on first visit, edits survive a round trip, the
  returned map carries both providers' latest values.
- `BlurtConfig.RefinementEndpoints` (enum-keyed map, structural equality,
  JSON round-trip covered in the store test). **Migration:** active
  `RefinementBaseUrl`/`RefinementModel` stay the runtime source of truth, so old
  configs load unchanged; the map starts empty and gets seeded from the live
  fields on the first switch — a custom URL is never lost.
- Settings UI: `OnRefinementProviderChanged` swaps the fields via Core's
  `Switch` (guarded against the initialization-time SelectionChanged); save
  persists the map with the visible fields as the active provider's values.
  The stored API key is untouched throughout (RefinerAuth gating from 17).

Verified live via UI Automation against the running window: OpenAI →
Local shows `http://localhost:11434/v1` / `llama3.1`; switching back restores
`https://api.openai.com/v1` / `gpt-4o-mini`. Suite green (176/176).

## Blocked by

- Best verified after issue 23 (Settings Save/Cancel crash) so a switch can be
  saved and reloaded. Builds on issue 17 (switchable provider).
