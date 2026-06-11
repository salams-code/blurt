# 18 — Use the selected Whisper model + per-selection download guidance

Status: ready-for-agent
Type: AFK logic (model/path resolution unit-tested) / HITL UI check

## Parent

[.scratch/blurt/PRD.md](../PRD.md)

## What to build

Use whatever Whisper model the user has selected, and tell them clearly how to
get it. Two parts of one slice:

1. **Bug:** transcriber provisioning is hardcoded to the default `small` model
   and ignores the configured choice, so the settings model selection has no
   effect. Load the model the config actually specifies; a change takes effect
   on restart (clearly communicated).
2. **Guidance:** because the in-app download is blocked by the corporate proxy,
   colleagues install the model file by hand. For the **currently selected**
   model, onboarding and settings must show the exact file to download, a working
   download link, and the target folder — so a manual install always matches what
   the app expects. Whatever is selected drives the expected filename, the link,
   and what gets loaded.

The model set should be sensible — keep `small` as the default and offer a
higher-quality option (e.g. `large-v3-turbo`); the unused `base` option can go —
but the core requirement is that the app loads whatever is selected and the
download guidance points at that exact file. Model/path resolution lives in
`Blurt.Core` and is unit-tested; the download link/folder shown in the UI is
derived from the selection, never hardcoded to one model.

## Acceptance criteria

- [ ] The transcriber loads the model selected in settings (not a hardcoded one); the choice persists and takes effect on restart, clearly communicated in the UI.
- [ ] For the currently selected model, onboarding and settings show the exact filename, a working download link, and the target folder, so a manual install matches what the app expects.
- [ ] Model filename/path resolution for any selection is unit-tested in `Blurt.Core`.
- [ ] No model is ever downloaded during build or tests.

## Blocked by

- None - can start immediately (builds on 04, 14, 15, all done).
