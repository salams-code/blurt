# Blurt

Native Windows tray app for push-to-talk voice dictation (.NET 8 / C#).
Design contract: [docs/superpowers/specs/2026-06-09-blurt-windows-design.md](docs/superpowers/specs/2026-06-09-blurt-windows-design.md).

## Build & test

The `dotnet` on PATH (`C:\Program Files\dotnet`) is runtime-only — no SDK. Use the user-profile SDK instead:

```powershell
$env:DOTNET_ROOT = "$env:USERPROFILE\.dotnet"
& "$env:USERPROFILE\.dotnet\dotnet.exe" test tests/Blurt.Core.Tests --nologo
```

Do not modify the system PATH or install SDKs machine-wide (corporate laptop, needs admin).

## Agent skills

### Issue tracker

Issues and PRDs live as local markdown files under `.scratch/<feature>/`. See `docs/agents/issue-tracker.md`.

### Triage labels

Five canonical triage roles, default label strings (no custom vocabulary). See `docs/agents/triage-labels.md`.

### Domain docs

Single-context repo: one `CONTEXT.md` + `docs/adr/` at the root. See `docs/agents/domain.md`.
