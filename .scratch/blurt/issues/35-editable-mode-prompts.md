# 35 — Editable prompts for every refined mode

Status: ready-for-agent
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

- [ ] Settings shows one editable prompt field per refined mode (Fix, English,
      Bullets, Custom), each pre-filled with its default.
- [ ] Editing a prompt and saving changes that mode's refinement on the next
      dictation, no restart.
- [ ] An unedited install behaves exactly as today (defaults unchanged).
- [ ] Pur remains promptless / zero-network.
- [ ] The defaults + per-mode config live in `Blurt.Core`, unit-tested; suite green.

## Blocked by

- None — can start immediately.
