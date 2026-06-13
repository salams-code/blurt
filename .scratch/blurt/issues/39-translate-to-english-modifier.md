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
local path. The extra modifier reuses the expanded hotkey vocabulary from issue 34.

## Acceptance criteria

- [ ] Holding the extra modifier during a refined dictation yields English output,
      on top of the active mode (verified for at least Bullets and Email).
- [ ] Without the modifier, behaviour is unchanged.
- [ ] It is per-dictation (no persisted setting); composes with any refined mode.
- [ ] Pur stays zero-network and unaffected by the modifier.
- [ ] The status overlay shows the layered operation.
- [ ] The compose/translate decision lives in `Blurt.Core`, unit-tested; suite green.

## Blocked by

- Issue 34 (expanded hotkey vocabulary) — the extra modifier needs hook support.
