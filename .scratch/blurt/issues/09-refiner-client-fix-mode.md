# 09 — Refiner client (OpenAI-compatible) + Fix mode

Status: ready-for-human
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

- [x] Tests run `IRefiner` against a mock OpenAI-compatible server (mock the server, not the client) and assert the request and the returned refined text.
- [x] Test: when the endpoint is unreachable, the pipeline inserts the raw text and surfaces a "refinement offline" notice.
- [ ] Fix hotkey end-to-end produces cleaned-up German text inserted at the cursor. *(manual — see handoff)*
- [x] Base URL, model, and key are read from settings; no audio is ever sent to the refiner.

## Blocked by

- 05 — Pur dictation end-to-end
- 08 — SettingsStore: JSON config + DPAPI-encrypted API key

## Implementation note (handoff)

**2026-06-11 (agent):** Built the refinement stage and wired the Fix hotkey.

What was built (all logic in `Blurt.Core`, unit-tested; HTTP is headless-testable
like DPAPI in issue 08):

- **`IRefiner`** (`src/Blurt.Core/Refiner.cs`) — `Task<string> RefineAsync(string text,
  string systemPrompt, CancellationToken ct)`. Only text crosses this seam, never audio.
- **`OpenAiCompatibleRefiner : IRefiner`** — backed by an injected `HttpClient`. Ctor
  `(HttpClient http, string baseUrl, string model, string apiKey)`. POSTs an OpenAI
  Chat Completions request (`system` = prompt, `user` = transcript) to
  `{baseUrl}/chat/completions`, returns the assistant `content`. The same client serves
  OpenAI cloud and a remote Ollama (only base URL/model/key differ). Empty key ⇒ no
  `Authorization` header (local Ollama needs none); a non-success status throws so the
  pipeline can fall back.
- **`RefinementPrompts.Fix`** (`src/Blurt.Core/RefinementPrompts.cs`) — German grammar/
  punctuation/filler-word cleanup prompt, German-only output, no translation/answer.
  (Intentionally **not** a `ModeRegistry` — that merges later in issue 11.)
- **Pipeline fallback** — `DictationPipeline` now wraps the refine step in try/catch:
  on failure it injects the **raw transcript** and returns the new
  `DictationOutcome.RefinedOffline`. The non-speech guard moved to before the refine
  call so an empty utterance never hits the network and a refiner failure can't
  resurrect a `[BLANK_AUDIO]` marker. Pur mode (`refine == null`) is unchanged.
- **App wiring** (`src/Blurt.App/TrayApplicationContext.cs`) — the Fix placeholder
  (`"hello from blurt"`) is replaced by real push-to-talk: `OnFixTrigger` (down=record,
  up=transcribe→refine→inject) + `FixAsync`. The refiner is built per utterance from
  `SettingsStore` (config base URL/model + DPAPI key) over one long-lived `HttpClient`.
  `RefinedOffline` ⇒ balloon "Refinement offline — raw text inserted."; no key still
  inserts the raw transcript (auth failure → fallback).

Tests: 9 new (`RefinementPromptsTests`, `OpenAiCompatibleRefinerTests`, 2 added to
`DictationPipelineTests`). Full Core suite green: **51 passed, 0 failed**. The refiner
tests use a fake `HttpMessageHandler` — no real network, no model download.
`build src/Blurt.App` green.

### Manual checks (human)

1. Launch Blurt (`build src/Blurt.App` then run `Blurt.exe`). Configure an OpenAI key
   (or point `RefinementBaseUrl`/`RefinementModel` at a reachable OpenAI-compatible
   endpoint, e.g. a remote Ollama) via the config/key store.
2. Focus a text field, hold **AltGr + ,**, speak a messy German sentence with filler
   words ("ähm, also ich wollte halt sagen…"), release. Expect cleaned-up German text
   inserted at the cursor (grammar/punctuation fixed, fillers removed).
3. With **no reachable endpoint** (wrong base URL / offline / no key), repeat: expect
   the **raw transcript** inserted plus the balloon "Refinement offline — raw text
   inserted." Nothing should be lost.
