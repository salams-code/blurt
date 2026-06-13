# Blurt

Push-to-talk voice dictation for Windows. Hold a hotkey, speak, release — Blurt
transcribes what you said and types it at the cursor in any app. A native tray
app (.NET 8 / C#), no window in your way.

## Download & run (portable)

1. Download `Blurt-0.1.0-win-x64.zip` from the [latest release](https://github.com/salams-code/blurt/releases/latest).
2. Extract the `Blurt` folder anywhere (e.g. `C:\Tools\Blurt`).
3. Double-click **`Blurt.exe`**.

No installer, no admin rights, and **no .NET needed** — the runtime is bundled.
Blurt lives in the system tray; right-click the tray icon for settings and recent
dictations.

> Keep the files together: `Blurt.exe` needs the `runtimes\` folder and the few
> `*.dll` next to it. Move the whole folder, not just the exe.

## How it works

Hold a hotkey to record, release to transcribe and inject:

| Hotkey | Mode |
| --- | --- |
| `AltGr` + `,` | **Fix** — clean up the dictation |
| `AltGr` + `.` | **English** — produce English text |
| `AltGr` + `-` | **Flex slot** — cycles Pur → Bullets → Custom |

- **Pur** is verbatim, fully offline, zero network — it always stays local.
- **Bullets** formats speech as bullet points; **Custom** runs your own prompt
  (e.g. translate, rephrase).

All hotkeys, the flex-slot order, the microphone, and the overlay are configurable
in **Settings** (tray → Settings).

## Privacy tiers

Settings frames the cloud choice by **what leaves your machine**:

| Tier | Voice (audio) | Text | 
| --- | --- | --- |
| **Fully local (offline)** | stays local | stays local (Ollama) |
| **Voice stays home** | stays local | → OpenAI (refinement only) |
| **Full cloud** | → OpenAI | → OpenAI |

Pur stays fully local regardless of tier. The cloud tiers need an OpenAI API key
(Settings → Refinement → API key), stored encrypted via Windows DPAPI — never in
plaintext.

## Transcription model

Local transcription uses whisper.cpp via Whisper.net. On first use Blurt downloads
the selected ggml model. If your network blocks the download (corporate proxy),
Settings shows the exact file, a copyable link, and the target folder for a manual
install.

## Start with Windows

Settings → Startup → *Start Blurt when Windows starts* adds a per-user entry (no
admin). If you move the Blurt folder, re-toggle it so it points at the new path.

## Build from source

The shipped binary is self-contained so it runs without .NET installed. Building
needs the .NET 8 SDK in your user profile (no machine-wide install):

```powershell
$env:DOTNET_ROOT = "$env:USERPROFILE\.dotnet"

# test
& "$env:USERPROFILE\.dotnet\dotnet.exe" test tests/Blurt.Core.Tests --nologo

# portable release: single Blurt.exe (managed + runtime bundled) plus the native
# whisper/WPF libraries on disk (kept out of the bundle so Whisper.net loads them).
& "$env:USERPROFILE\.dotnet\dotnet.exe" publish src/Blurt.App/Blurt.App.csproj `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=false `
    -p:EnableCompressionInSingleFile=true -p:DebugType=none `
    -o publish/Blurt-Portable
```

Verify the native whisper libraries load in a build: `Blurt.exe --selftest` writes
PASS/FAIL to `%TEMP%\blurt-selftest.txt`.

See [CLAUDE.md](CLAUDE.md) for the full developer setup.
