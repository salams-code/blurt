# 09 — Refiner client (OpenAI-compatible) + Fix mode

Status: ready-for-agent
Type: AFK

## Parent

[.scratch/blurt/PRD.md](../PRD.md)

## What to build

The refinement stage and the first refined mode. A single `IRefiner` backed by an
OpenAI-compatible HTTP client with configurable base URL + model + API key
(the same client serves OpenAI cloud and a remote Ollama instance with no code
change). Default endpoint OpenAI, model `gpt-4o-mini`. Wire the "Fix" hotkey
(`AltGr + ,`): transcribe → refine with the Fix prompt (grammar/punctuation/
filler-word cleanup, German output) → inject. Only text (never audio) is sent to
the endpoint. If the endpoint is unreachable, fall back to inserting the raw
transcription plus a notice. Candidate for ADR-0002 (single OpenAI-compatible
refiner client).

## Acceptance criteria

- [ ] Tests run `IRefiner` against a mock OpenAI-compatible server (mock the server, not the client) and assert the request and the returned refined text.
- [ ] Test: when the endpoint is unreachable, the pipeline inserts the raw text and surfaces a "refinement offline" notice.
- [ ] Fix hotkey end-to-end produces cleaned-up German text inserted at the cursor.
- [ ] Base URL, model, and key are read from settings; no audio is ever sent to the refiner.

## Blocked by

- 05 — Pur dictation end-to-end
- 08 — SettingsStore: JSON config + DPAPI-encrypted API key
