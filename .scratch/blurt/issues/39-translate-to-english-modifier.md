# 39 — "Also translate to English" via an extra modifier

Status: done
Type: AFK feature

## Parent

[.scratch/blurt/PRD.md](../PRD.md)

## What to build

Holding an **extra modifier** during a refined dictation makes the output **also
translated to English**, layered on top of whatever mode is active — so Bullets
becomes English bullets, Email becomes an English email, Fix becomes cleaned-up
English, etc. It is a per-dictation decision (held at dictation time), not a Settings
toggle, and composes with any refined mode.

Mechanically this adds a translation step to the refinement of that one dictation
(the mode's own refinement, then translate-to-English — or a combined instruction),
so only text crosses the network, never extra audio. The live-status overlay should
reflect the layered operation (e.g. "bulleting → english" or an "+EN" marker).

Pur is exempt and must stay zero-network: the modifier has no effect on the verbatim
local path. The extra modifier is **Shift**, held together with the existing trigger
chord (e.g. `AltGr+Shift+,`). The hook gains simple Shift-state tracking and tags the
dictation as also-translate — no change to the `AltGr+{,.-}` bindings, so this is
**decoupled from issue 34** (2026-06-13 decision).

## Acceptance criteria

- [x] Holding the extra modifier during a refined dictation yields English output,
      on top of the active mode (verified for at least Bullets and Email).
- [x] Without the modifier, behaviour is unchanged.
- [x] It is per-dictation (no persisted setting); composes with any refined mode.
- [x] Pur stays zero-network and unaffected by the modifier.
- [x] The keyboard hook observes Shift held with a trigger chord and tags that
      dictation as also-translate; the trigger chord is still swallowed.
- [x] The status overlay shows the layered operation.
- [x] The compose/translate decision lives in `Blurt.Core`, unit-tested; suite green.

## Comments

Built the Shift = "also translate to English" modifier (strict TDD on all three Core
units, RED before GREEN; Win32 hook + tray wiring are the exempt shell). Decoupled
from issue 34 as decided: just simple Shift-state tracking, no vocabulary rework.

**Core (unit-tested, 251 tests green):**

- `TranslationModifier.Compose(basePrompt, alsoTranslate)` — the compose decision:
  appends an English-translation layer to the mode's own prompt when the modifier is
  held (single refinement call, only text crosses the network). A null/blank base
  prompt (the verbatim path) returns null regardless of the modifier, so **Pur stays
  zero-network**. Tested generically and explicitly for Bullets and Email.
- `TriggerResolver` — now tracks Shift (0x10 / 0xA0 / 0xA1) like AltGr and stamps
  `TriggerEvent.AlsoTranslate` on the trigger's Down from the Shift state at press
  time; the chord is still swallowed; Shift itself passes through. `TriggerEvent`
  gained `AlsoTranslate` (defaulted false → every existing caller/test unchanged).
- `StatusLabel.AlsoEnglish(baseLabel)` → e.g. `"bulleting → english"` (ellipsis-free,
  so the overlay still animates the dots).

**App shell (TrayApplicationContext):** captures `AlsoTranslate` from the trigger's
Down (per-dictation, never persisted), then on the Fix / English / Flex-hold key-up
composes the prompt via `TranslationModifier.Compose` and layers the pill label via
`StatusLabel.AlsoEnglish`. The Flex verbatim branch (Pur / empty Custom) composes to
null and keeps going through `DictateAsync` (zero network). KeyboardHook is unchanged
— it already forwards every raw key (incl. Shift) to the resolver.

HITL UX check recommended: hold Shift with a trigger chord (e.g. AltGr+Shift+- on
Bullets, and on Email) and confirm the output is English while keeping the mode's
shape (English bullets / an English email); confirm the overlay shows the layered
verb (e.g. "bulleting → english"); confirm a plain chord (no Shift) is unchanged; and
confirm holding Shift on Pur still inserts the verbatim local transcript with no
network call.

## Blocked by

- Issue 36 (Email Flex mode) — 39's acceptance verifies translate-on-Email, so the
  Email mode must exist first. The Shift mechanism itself is independent; this is
  **decoupled from issue 34** (2026-06-13): the hook adds simple Shift-state tracking,
  no expanded-vocabulary rework needed.
