# 37 — Reset prompts to defaults, with a backup

Status: done
Type: AFK feature

## Parent

[.scratch/blurt/PRD.md](../PRD.md)

## What to build

A **Reset** action in Settings that returns all editable prompts (and the Custom
mode's name) to our shipped defaults — but never destructively. Before resetting,
it **backs up** the user's current prompts + names into a stored snapshot, then
applies the defaults. So a reset is always recoverable.

Only the most recent pre-reset state needs to survive (a single backup slot,
overwritten on each reset). The backup is persisted alongside the config. This slice
is the create-the-backup-and-reset half; surfacing/restoring the backup in the UI is
issue 38.

## Acceptance criteria

- [x] A Reset action sets every editable prompt (and Custom name) back to its default.
- [x] Before resetting, the current prompts + names are captured into a persisted
      backup snapshot (overwriting any previous one).
- [x] Reset is non-destructive: the pre-reset values are fully recoverable from the
      backup.
- [x] Resetting with nothing customised is a safe no-op.
- [x] Backup/reset logic lives in `Blurt.Core`, unit-tested; suite green.

## Blocked by

- Issue 35 (editable per-mode prompts) — there must be editable prompts to reset/back up.

## Comments

Built the non-destructive prompt reset + single backup slot (strict TDD: reset and
backup behaviours observed RED before implementing; UI shell exempt).

**Scope note — "Custom mode name":** Blurt has no renamable Custom *mode name*
anywhere in the codebase or PRD (only a Custom *prompt*). I did not invent a rename
feature; the editable surface is the five mode prompts (Fix / English / Bullets /
Email / Custom), and the reset + backup cover all of it.

**Core (unit-tested, 240 tests green):**

- `PromptSnapshot` — an init-only record of the five editable prompts, with
  `From(config)` (capture) and `ApplyTo(config)` (restore — the seam issue 38's
  restore UI will use). Round-trips through `SettingsStore` JSON like
  `RefinementEndpoint`; a config written before this slot deserialises to `null`.
- `BlurtConfig.PromptBackup` — the single nullable backup slot (in Equals/GetHashCode;
  round-trips; absent key → null, backward-compatible).
- `PromptReset.Reset(config)` — backs the current prompts up into `PromptBackup`
  (overwriting any previous), then sets every prompt to its `ModePrompts.DefaultFor`
  default. **Safe no-op when nothing is customised**: returns the config unchanged so
  a redundant reset can't clobber a real backup with a useless default snapshot.

**App shell:** a "Reset prompts to defaults" link button in the Mode-prompts card
calls `PromptReset.Reset` on the current field values, reflects the defaults back into
the prompt fields, and holds the backup in `_promptBackup` (persisted with the config
on Save). The restore/surfacing UI is deferred to issue 38, as the issue specifies.

HITL UX check recommended: open Settings, edit a couple of prompts, click "Reset
prompts to defaults" and confirm all prompt fields (including Custom) revert to the
shipped wording; Save, reopen Settings, and confirm the defaults stuck. (The backup
itself isn't surfaced yet — that's issue 38; for now it can be eyeballed in
`%APPDATA%\Blurt\config.json` under `PromptBackup`.)
