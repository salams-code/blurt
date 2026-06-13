# 39 — "Also translate to English" via an extra modifier

Status: ready-for-agent
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

- [ ] Holding the extra modifier during a refined dictation yields English output,
      on top of the active mode (verified for at least Bullets and Email).
- [ ] Without the modifier, behaviour is unchanged.
- [ ] It is per-dictation (no persisted setting); composes with any refined mode.
- [ ] Pur stays zero-network and unaffected by the modifier.
- [ ] The keyboard hook observes Shift held with a trigger chord and tags that
      dictation as also-translate; the trigger chord is still swallowed.
- [ ] The status overlay shows the layered operation.
- [ ] The compose/translate decision lives in `Blurt.Core`, unit-tested; suite green.

## Blocked by

- Issue 36 (Email Flex mode) — 39's acceptance verifies translate-on-Email, so the
  Email mode must exist first. The Shift mechanism itself is independent; this is
  **decoupled from issue 34** (2026-06-13): the hook adds simple Shift-state tracking,
  no expanded-vocabulary rework needed.
