# 37 — Reset prompts to defaults, with a backup

Status: ready-for-agent
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

- [ ] A Reset action sets every editable prompt (and Custom name) back to its default.
- [ ] Before resetting, the current prompts + names are captured into a persisted
      backup snapshot (overwriting any previous one).
- [ ] Reset is non-destructive: the pre-reset values are fully recoverable from the
      backup.
- [ ] Resetting with nothing customised is a safe no-op.
- [ ] Backup/reset logic lives in `Blurt.Core`, unit-tested; suite green.

## Blocked by

- Issue 35 (editable per-mode prompts) — there must be editable prompts to reset/back up.
