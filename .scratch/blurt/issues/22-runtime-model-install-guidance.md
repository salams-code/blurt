# 22 — Runtime model-load failure shows manual-install guidance

Status: ready-for-agent (found in HITL test, 2026-06-12)
Type: AFK (thin runtime notice over existing Core logic) / HITL check

## Parent

[.scratch/blurt/PRD.md](../PRD.md)

## What's broken

When the selected Whisper model isn't on disk and the in-app download is blocked
(corporate proxy), the **runtime** path on first dictation
(`TrayApplicationContext.ProvisionTranscriberAsync`) only says "Downloading… ~460 MB"
and then a generic "Dictation unavailable: <message>". Issue 18 already built the
per-selection manual-install guidance (`WhisperModel.DownloadUrl`,
`ModelProvisioner.ModelsDirectory`, the onboarding/settings `ManualInstallHint`) —
but the runtime notice doesn't use it, so a blocked user isn't told the exact file,
link, and folder to install by hand. The "Downloading…" notice must also name the
**selected** model, not a hardcoded one.

## What to build

- The first-run "Downloading…" notice names the **selected** model
  (`config.WhisperModel.FileName`), not a hardcoded model.
- On a provisioning/download failure, the fail-soft notice points at the manual
  install: the exact filename, the working download link (`WhisperModel.DownloadUrl`),
  and the target folder (`ModelProvisioner.ModelsDirectory`) — the same guidance
  the settings/onboarding UI shows (issue 18), so a hand install matches what the
  app loads. Reuse the existing Core-derived strings; no new model logic.
- No model is ever downloaded during build or tests.

## Acceptance criteria

- [ ] The "downloading" and failure notices reference the currently selected model's exact file, link, and folder.
- [ ] The failure path stays fail-soft (no crash); the app keeps running.
- [ ] No model downloaded during build/tests; the suite stays green; the app builds.

## Blocked by

- None (builds on 18, done).
