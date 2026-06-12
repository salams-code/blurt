# 25 — Manual-install guidance must be copyable (download link + target folder)

Status: ready-for-human (implemented 2026-06-12, agent-verified via UIA; awaiting HITL UX check)
Type: AFK App UI (thin shell over existing Core strings) / HITL UX check

## Parent

[.scratch/blurt/PRD.md](../PRD.md)

## What's wrong (HITL finding, 2026-06-12)

The per-selection manual-install guidance in Settings (issue 18) — and the runtime
failure notice (issue 22) — show the exact model **filename, download URL, and
target folder**, but the text is a non-selectable WPF `TextBlock`
(`ModelDownloadHint` in [SettingsWindow.xaml](../../../src/Blurt.App/SettingsWindow.xaml)).
The user can't select or copy any of it. In the corporate-proxy scenario the whole
point is to **hand-install the file**, which means copying the URL into a browser
and the folder path into Explorer — currently impossible without retyping a long
huggingface URL and an AppData path by hand.

## What to build

- Make the download URL and target-folder path **copyable** wherever the
  manual-install guidance is shown (Settings model hint, onboarding, and the
  runtime failure notice if it surfaces a copyable surface).
- Minimal: render the hint with selectable text (e.g. a read-only `TextBox`/
  `SelectableTextBlock`, or a `Hyperlink` for the URL). Nicer: a small "Copy link"
  / "Copy folder" affordance next to it.
- Reuse the existing Core-derived strings (`WhisperModel.DownloadUrl`,
  `ModelProvisioner.ModelsDirectory`) — no new model logic; this is presentation.

## Acceptance criteria

- [x] The download link and target folder shown in the model-install guidance can be selected/copied (no retyping).
- [x] Works for the currently selected model (link/folder still derived from the selection, per issue 18).
- [x] The suite stays green; the app builds.

## Comments

**2026-06-12 (agent):** Implemented as "Copy link" / "Copy folder" link-buttons
(LinkButton style from the issue-19 theme) under the hint in **Settings**
(`OnCopyModelLink`/`OnCopyModelFolder`, derived from the live combo selection)
and in the **onboarding model step** (`ModelCopyPanel`, shown exactly when the
manual-install guidance is shown: model missing, or download failed). Shared
`ClipboardCopy.WithFeedback` flashes "Copied ✓" and fails soft if the clipboard
is locked. The runtime failure notice (issue 22) stays a balloon — no copyable
surface there; its copy path is Settings.

Verified: clicked both buttons via UI Automation — clipboard contained
`https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small-q5_1.bin`
and the models folder `%APPDATA%\Blurt\models`. Suite green (163/163).

## Blocked by

- None. Builds on issues 18 and 22 (both done). Pairs well with issue 19 (visual
  polish) if the copy affordance is styled.
