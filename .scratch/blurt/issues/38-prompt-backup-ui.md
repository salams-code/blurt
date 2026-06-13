# 38 — See, copy, and restore the prompt backup from the UI

Status: ready-for-agent
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

- [ ] The UI indicates whether a backup exists.
- [ ] The user can view the backed-up prompts + names.
- [ ] The user can copy the backup to the clipboard.
- [ ] The user can restore the backup into the active prompts; it takes effect on
      the next dictation (no restart).
- [ ] Pure backup read/restore logic lives in `Blurt.Core`, unit-tested; suite green.

## Blocked by

- Issue 37 (reset creates the backup) — there must be a backup to view/copy/restore.
