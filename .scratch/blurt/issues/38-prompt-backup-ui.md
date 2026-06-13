# 38 — See, copy, and restore the prompt backup from the UI

Status: done (HITL soft-pass 2026-06-14 — user accepted; full Copy/Restore round-trip not eyeballed)
Type: AFK feature

## Parent

[.scratch/blurt/PRD.md](../PRD.md)

## What to build

Make the prompt backup (created by a reset, issue 37) visible and usable from the
Settings UI. The user can see that a backup exists, view its contents, copy them to
the clipboard, and restore the backup back into the live prompts.

- A clear indicator when a backup is present (and when there is none).
- View the backed-up prompts + names.
- Copy the backup to the clipboard.
- Restore: put the backed-up prompts + names back as the active configuration
  (applies without a restart, like other prompt edits).

## Acceptance criteria

- [x] The UI indicates whether a backup exists.
- [x] The user can view the backed-up prompts + names.
- [x] The user can copy the backup to the clipboard.
- [x] The user can restore the backup into the active prompts; it takes effect on
      the next dictation (no restart).
- [x] Pure backup read/restore logic lives in `Blurt.Core`, unit-tested; suite green.

## Blocked by

- Issue 37 (reset creates the backup) — there must be a backup to view/copy/restore.

## Comments

Surfaced the prompt backup (created by issue 37's reset) in the Settings UI: see /
copy / restore (strict TDD on the Core logic; UI shell exempt).

**Scope note — "+ names":** as in issues 36/37, Blurt has no separate editable mode
*name*, so the "names" shown are the mode names (Fix / English / Bullets / Email /
Custom) used as labels next to each backed-up prompt.

**Core (unit-tested, 242 tests green):**

- `PromptBackupText.Format(snapshot)` — the single labelled, multi-line rendering used
  for *both* the on-screen view and the clipboard, so they always match. Each prompt
  is shown under its mode name.
- Restore is `PromptSnapshot.ApplyTo` (from issue 37): a test pins that a restored
  backup becomes the active prompts as resolved through `ModePrompts.For` — i.e. it
  takes effect on the next dictation (prompts are read per-take), no restart.

**App shell (new "Prompt backup" card in Settings):**

- A status line: "A backup of your previous prompts is available…" vs "No backup yet…".
- A read-only box showing `PromptBackupText.Format` (collapsed when no backup).
- "Copy backup" → clipboard via the existing `ClipboardCopy.WithFeedback`.
- "Restore backup" → loads the backed-up prompts back into the prompt fields (Core's
  `ApplyTo`); persisted on Save. The backup slot is kept so the user can restore again.
  Copy/Restore are disabled when no backup exists; the view refreshes after a reset.

HITL UX check recommended: in Settings, with no reset done yet, confirm the card says
"No backup yet" and Copy/Restore are disabled. Then edit a prompt, click "Reset prompts
to defaults", and confirm the card now shows the backed-up prompts; try "Copy backup"
(paste elsewhere to verify) and "Restore backup" (the prompt fields should revert to the
backed-up wording); Save and confirm a dictation uses the restored prompt.
