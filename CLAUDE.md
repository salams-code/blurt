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

Publish **single-file with `IncludeNativeLibrariesForSelfExtract=false`**: managed assemblies + the runtime bundle into one `Blurt.exe`, but the native libraries stay on disk next to it. That last part is load-bearing — Whisper.net finds its native `runtimes/win-x64/*.dll` by probing that folder; with the natives self-extracted into the bundle's temp dir Whisper.net **cannot** find them and local transcription (Pur) breaks. Verified via `Blurt.exe --selftest`.

```powershell
$env:DOTNET_ROOT = "$env:USERPROFILE\.dotnet"
& "$env:USERPROFILE\.dotnet\dotnet.exe" publish src/Blurt.App/Blurt.App.csproj `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=false `
    -p:EnableCompressionInSingleFile=true -p:DebugType=none `
    -o publish/Blurt-Portable
```

Output: `publish/Blurt-Portable/` (git-ignored) — `Blurt.exe` (~65 MB) + a few native WPF DLLs + a `runtimes/` folder, ~18 files total. Ship the folder zipped. (Do **not** set `IncludeNativeLibrariesForSelfExtract=true`/true single-file: it produces one exe but breaks native whisper loading.)

`Blurt.exe --selftest` loads an installed ggml model through `WhisperFactory` and writes PASS/FAIL/SKIP to `%TEMP%\blurt-selftest.txt` (exit 0/1/2) — the native-load smoke test for a portable build.

## Agent skills

### Issue tracker

Issues and PRDs live as local markdown files under `.scratch/<feature>/`. See `docs/agents/issue-tracker.md`.

### Triage labels

Five canonical triage roles, default label strings (no custom vocabulary). See `docs/agents/triage-labels.md`.

### Domain docs

Single-context repo: one `CONTEXT.md` + `docs/adr/` at the root. See `docs/agents/domain.md`.
