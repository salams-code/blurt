# 17 — Switchable refinement provider with persistent key

Status: ready-for-agent
Type: AFK logic (provider/key-application unit-tested) / HITL UI check

## Parent

[.scratch/blurt/PRD.md](../PRD.md)

## What to build

Let the user switch the refinement endpoint between the OpenAI cloud and a
local/remote OpenAI-compatible endpoint (e.g. Ollama) **without deleting the
stored API key**. Today the OpenAI-compatible client only attaches the
`Authorization` header when a key is set — so a keyless Ollama endpoint already
works — but the only way to use it is to clear the saved key, which is annoying
when switching back and forth. Keep the key stored (DPAPI) and make the active
endpoint a setting; the key is sent only when the active provider needs it.

Also document the local/Ollama path in-app so a user without an OpenAI key knows
what to do: the onboarding key step presents the local/Ollama option as an
alternative, and the settings refinement section shows clear guidance (e.g. base
URL `http://<host>:11434/v1`, key left empty).

The provider-selection and key-application decision lives in `Blurt.Core` and is
unit-tested (when is the key sent, which endpoint is active); the settings UI is
a thin layer over it.

## Acceptance criteria

- [ ] The user can switch between the OpenAI endpoint and a local/Ollama endpoint without deleting the stored API key; the key persists (DPAPI) across the switch.
- [ ] The key is sent only when the active provider requires it; a keyless local endpoint works while a key is still stored.
- [ ] Onboarding's key step presents the local/Ollama option as an alternative to an OpenAI key.
- [ ] Settings shows clear guidance for pointing at a local/Ollama endpoint (base URL + empty key).
- [ ] Provider-selection / key-application logic is unit-tested in `Blurt.Core`.

## Blocked by

- None - can start immediately (builds on 09, 14, both done).
