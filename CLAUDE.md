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

If `~/.dotnet` is empty (fresh machine — only the .NET 7 *runtime* ships on PATH), bootstrap the .NET 8 SDK into the user profile, no admin needed:

```powershell
Invoke-WebRequest https://dot.net/v1/dotnet-install.ps1 -OutFile $env:TEMP\dotnet-install.ps1
& $env:TEMP\dotnet-install.ps1 -Channel 8.0 -InstallDir "$env:USERPROFILE\.dotnet" -NoPath
```

### Portable release

The shipped binary must run by double-click on any Windows x64 box with no .NET installed, so publish **self-contained** (bundles the runtime). A plain framework-dependent build only runs when `DOTNET_ROOT` points at the SDK.

```powershell
$env:DOTNET_ROOT = "$env:USERPROFILE\.dotnet"
& "$env:USERPROFILE\.dotnet\dotnet.exe" publish src/Blurt.App/Blurt.App.csproj -c Release -r win-x64 --self-contained true -o publish/Blurt
```

Output lands in `publish/Blurt/Blurt.exe` (~166 MB folder, git-ignored).

## Agent skills

### Issue tracker

Issues and PRDs live as local markdown files under `.scratch/<feature>/`. See `docs/agents/issue-tracker.md`.

### Triage labels

Five canonical triage roles, default label strings (no custom vocabulary). See `docs/agents/triage-labels.md`.

### Domain docs

Single-context repo: one `CONTEXT.md` + `docs/adr/` at the root. See `docs/agents/domain.md`.
