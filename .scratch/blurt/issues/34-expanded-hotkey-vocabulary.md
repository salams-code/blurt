# 34 — Expanded hotkey vocabulary (more than AltGr+{ , . - })

Status: wontfix (2026-06-14 — user decision: not needed)
Type: HITL design + implementation

## Parent

[.scratch/blurt/PRD.md](../PRD.md)

## What to build

Today a trigger can only be bound to `AltGr + { , . - }` — three keys, one
modifier. Broaden the binding vocabulary so a user can bind each trigger (Fix,
English, Flex) to a wider set of keys/modifiers of their choosing, and persist the
choice. Existing `AltGr+,` / `AltGr+.` / `AltGr+-` bindings must keep working
unchanged (migration-free).

This slice owns the **design decision** as well as the implementation:

- Which keys are bindable (e.g. AltGr + any letter/number, or additional modifier
  combinations like Ctrl+Alt+key)?
- How the Settings / Onboarding capture field records a wider chord (it currently
  only accepts AltGr + the three OEM characters).
- Conflict handling: two triggers must not share a chord; a chord must not clobber
  a common OS/app shortcut without warning.
- The keyboard hook must still **swallow** the trigger chord so the character never
  reaches the focused app, for the whole expanded set.

(Originally the enabler for issue 39's translate layer; **39 was decoupled on
2026-06-13** to use Shift directly as its extra modifier, so 34 no longer blocks
anything — it is a standalone enhancement now.)

## Acceptance criteria

- [ ] A trigger can be bound to a key/modifier combination outside the old
      `AltGr+{,.-}` set, captured in Settings and persisted.
- [ ] The bound chord is swallowed by the hook (does not type into the focused app).
- [ ] Two triggers cannot be bound to the same chord; the UI surfaces the clash.
- [ ] Existing persisted `AltGr+,` / `.` / `-` bindings still resolve and fire.
- [ ] Pure binding/parse/conflict logic lives in `Blurt.Core`, unit-tested; suite green.

## Blocked by

- None — can start immediately.

## Comments

**2026-06-14 (user decision) — wontfix.** Originally the enabler for issue 39's
translate layer, but 39 was decoupled (2026-06-13) to use Shift directly, so 34 no
longer blocks anything. The existing `AltGr+{,.-}` vocabulary is sufficient for the
shipped triggers; an expanded binding set is not wanted at this time. Can be reopened
later if a user actually needs custom chords.
