# 28 — Clarify that the API key also powers Online transcription (quick UI fix)

Status: superseded by issue 27 (closed 2026-06-13)
Type: App UI (label/hint only) — quick win, ships before issue 27's redesign

## Parent

[.scratch/blurt/PRD.md](../PRD.md)

## What's confusing (user, 2026-06-13)

The single stored OpenAI key lives behind the **"Refinement (LLM)"** card's
"API key" field
([SettingsWindow.xaml:122-132](../../../src/Blurt.App/SettingsWindow.xaml#L122-L132)),
but the **same key also feeds Online transcription** — the Whisper API call uses
`_settings.LoadApiKey()` against `https://api.openai.com/v1`
([TrayApplicationContext.cs:539-554](../../../src/Blurt.App/TrayApplicationContext.cs#L539-L554)).

So a user who sets **Transcription Source = Online** but leaves the refinement
provider on Local has no obvious cue that they must still enter the key under
"Refinement" for transcription to work. The field is mis-located relative to what
it actually powers.

## Fix (small, label/hint only — no logic change)

- Reword the API-key hint to state it powers **both** OpenAI refinement **and**
  Online transcription, and/or
- Surface the requirement near the Transcription "Source = Online" control too
  (e.g. an inline note pointing to the single key field).

No Core change; the key plumbing already works. This is purely making the existing
behaviour discoverable.

## Acceptance criteria (draft)

- [ ] When Online transcription is selected, the UI makes clear the OpenAI API key
      (the one in the Refinement card) is required for it.
- [ ] The API-key hint states it serves both transcription and refinement.
- [ ] No behavioural/logic change; suite stays green; app builds.
- [ ] HITL UX check.

## Related

- Stop-gap for the current two-dropdown UI. **Issue 27** (guided privacy tiers)
  reframes this entirely and supersedes this hint once shipped — keep this small
  so it isn't wasted effort if 27 lands soon.

## Comments

**2026-06-13 (agent) — superseded, not built.** Issue 27 shipped the same session
and reframes the whole transcription/refinement choice as a guided privacy tier.
The concern here is now covered by 27's per-tier hint: the "Voice stays home" and
"Full cloud" tiers both state **"Needs the API key below"**, which is exactly the
"the key also powers online transcription" cue this issue asked for. No separate
hint was added (it would have been immediately replaced). Closing as superseded.
