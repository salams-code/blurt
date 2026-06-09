# 10 — English mode (translate to English)

Status: ready-for-agent
Type: AFK

## Parent

[.scratch/blurt/PRD.md](../PRD.md)

## What to build

The "English" hotkey (`AltGr + .`): transcribe the (German) speech, then refine
with a translation prompt that returns clean English, and inject. Reuses the
`IRefiner` and pipeline from 09 with a different mode prompt registered in
`ModeRegistry`.

## Acceptance criteria

- [ ] Test: the English mode prompt is selected and sent for the English hotkey, asserted against the mock server.
- [ ] End-to-end: speaking German with the English hotkey inserts fluent English text.
- [ ] Same fail-soft fallback to raw text applies when the endpoint is unreachable.

## Blocked by

- 09 — Refiner client (OpenAI-compatible) + Fix mode
