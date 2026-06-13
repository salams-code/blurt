# 27 — Guided privacy-tier selector for transcription + refinement ("own your voice")

Status: ready-for-human (implemented 2026-06-13, TDD Core + UIA-verified shell; awaiting HITL UX sign-off)
Type: App UI (guided selector) + small Core (tier ⇄ {TranscriptionMode, RefinementProvider} mapping, unit-tested) + HITL UX check

## Parent

[.scratch/blurt/PRD.md](../PRD.md)

## Motivation (user, 2026-06-13)

Today the privacy-relevant choice is split across **two technical dropdowns** in
Settings — Transcription Source (Local / Online) and Refinement Provider
(OpenAI / Local Ollama) — plus the implicit "Pur is always local, zero network"
contract. The user's mental model is different and clearer: **what leaves my
machine?** The real trust boundary is **audio vs. text**, not "local vs. cloud".

A user who cares about "own your voice" wants to decide consciously: keep my
**voice** on the machine and let only the **transcribed text** be refined in the
cloud — that's exactly `Local transcription + OpenAI refinement` today, but
nothing in the UI frames it as that deliberate choice.

## Idea: present a privacy tier as the primary control

Surface a single guided choice, with the two existing dropdowns demoted to an
"Advanced" disclosure that the tier drives:

```
Stufe 0 — Fully local / offline      Audio: local   Text: local (Ollama)   "nothing leaves your machine"
Stufe 1 — Voice stays home           Audio: LOCAL    Text: → OpenAI         "only the text leaves" ◀ own-your-voice
Stufe 2 — Full cloud                 Audio: → OpenAI Text: → OpenAI         "fastest/best transcription"
```

- Each tier shows an explicit, plain-language line: **"Deine Stimme verlässt den
  Rechner: ja / nein."**
- **Pur stays hard-wired to Stufe 0 by design contract** regardless of the
  selected tier — `zeroNetwork: true` in `DictateAsync` is the guarantee and must
  not be weakened. The tier selector governs only the refined modes (Fix /
  English / Bullets / Custom).
- Choosing a tier that sends text/audio to OpenAI makes the **API-key requirement
  explicit in context** (supersedes the standalone hint from issue 28).

## Design notes

- The tier → (TranscriptionMode, RefinementProvider) mapping is a **pure Core
  function**, unit-tested; the Settings window is the thin shell that reads/writes
  it (same split as issues 17 / 24). Selecting a tier sets both underlying values;
  the Advanced dropdowns remain the escape hatch for non-standard combos (e.g.
  Online transcription + local refinement) and, when used, the tier shows as
  "Custom".
- No engine change: the two axes already exist and are independent
  ([TrayApplicationContext.cs:347](../../../src/Blurt.App/TrayApplicationContext.cs#L347)
  for the Pur zero-network path,
  [TrayApplicationContext.cs:409-433](../../../src/Blurt.App/TrayApplicationContext.cs#L409-L433)
  for "only text crosses the network" in refined modes). This issue is framing +
  a thin mapping, not new transcription/refinement mechanics.
- Onboarding (issue 15) should stay consistent — ideally the same tier language at
  first run.

## Acceptance criteria (draft — refine in triage)

- [x] Settings presents a primary privacy-tier choice (0/1/2) with an explicit
      "what leaves this PC" line per tier.
- [x] Selecting a tier sets the underlying TranscriptionMode + RefinementProvider;
      the source/model controls live under an "Advanced" disclosure and reflect the tier.
- [x] A non-standard combo (Online + local refiner) surfaces as "Custom" rather than misreporting a tier.
- [x] Pur remains zero-network / local regardless of the selected tier (contract untouched — no engine change).
- [x] Tiers requiring OpenAI make the API-key requirement explicit in the per-tier hint.
- [x] tier ⇄ {mode, provider} mapping is unit-tested in `Blurt.Core`; suite green (191); app builds clean.
- [ ] HITL UX sign-off against the running Settings window (screenshots captured; awaiting the user's call on wording/order).

## Related

- Builds on issue 12 (online transcription), issue 17 (switchable refinement
  provider), issue 24 (per-provider endpoint memory).
- Supersedes the standalone key-clarity hint in issue 28 once shipped.

## Deferred (explicitly out of scope here)

Streaming / chunked transcription and live incremental insertion were discussed
and **deliberately postponed** (user, 2026-06-13): decide on speed work only after
experiencing OpenAI refinement in real use. Not part of this issue.

## Comments

**2026-06-13 (agent) — built (TDD Core + manually-verified shell):**

- **Core `PrivacyTiers`** (`src/Blurt.Core/PrivacyTiers.cs`): `PrivacyTier`
  enum (FullyLocal / VoiceStaysHome / FullCloud) + pure `SettingsFor(tier)` →
  (TranscriptionMode, RefinementProvider) and `Classify(mode, provider)` →
  `PrivacyTier?` (null = Custom). Mapping: FullyLocal=(Local, Local),
  VoiceStaysHome=(Local, OpenAi), FullCloud=(Online, OpenAi). TDD, one vertical
  slice per cycle; 8 tests (`PrivacyTiersTests`), incl. round-trip and the
  non-standard→Custom case. Full suite 183→191, green.
- **Settings UI** (chosen layout: tier in the Transcription card): `PrivacyTierBox`
  is the primary control with a per-tier "what leaves this PC" hint; the Source +
  Local-model controls moved under an "Advanced" disclosure (CheckBox toggle — no
  Expander style in the theme). Selecting a tier sets both axes (and reuses issue
  24's provider→endpoint swap); a manual source/provider edit re-derives the tier
  or falls to "Custom" (auto-revealing Advanced on load of a Custom config). A
  `_syncingTier` guard breaks the SelectionChanged feedback loop. **No `BlurtConfig`
  change** — the tier is derived from the persisted axes, so no migration.
- **Verified live** via UI Automation against the running window (read-back through
  SelectionPattern, not pixels): all three tiers set the right (source, provider,
  base URL, model), and Online+local-refiner classifies as Custom. Screenshots in
  `.scratch/blurt/screenshots/tier-*.png`.

Open: HITL sign-off on label wording ("Voice stays home (cloud refine)", "Full
cloud") and whether the tier belongs in the Transcription card vs. its own card.

## Related (cont.)

- Builds on issue 24's `OnRefinementProviderChanged` endpoint swap, which the tier
  reuses when it sets the provider.
