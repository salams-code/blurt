# Handoff — Blurt (2026-06-13)

Context for continuing in a fresh session. Read this first.

## Where things stand

Working on **v0.1.0 — first portable release** of Blurt. Two features built this
session, release packaged locally, **nothing published**. A real bug was found
(issue 30) that should be looked at before/with the release.

### Git state (IMPORTANT)
- Branch `main`. **Nothing is pushed** — `origin/main` is ~10 commits behind,
  including ALL recent work (issues 12–29). The new code is **local-only**, so it
  is **not public** regardless of repo visibility.
- This session added 2 commits (local):
  - `ff0210c` Issues 27 + 29 (privacy-tier selector + start-with-Windows)
  - `edecab1` Release v0.1.0 (portable single-file build, --selftest, README, version)
- **`gh` CLI is NOT installed** → can't query repo visibility or create a GitHub
  release via CLI. A release needs `gh` installed first, or the GitHub web UI.
- **User has NOT authorized publishing.** Do not `git push` / release without an
  explicit go.

## Done this session
- **Issue 27** — guided privacy-tier selector. Core `PrivacyTiers` (enum +
  `SettingsFor`/`Classify`, 8 TDD tests, suite 183→191). Settings: "Privacy"
  combo (Fully local / Voice stays home / Full cloud / Custom) in the Transcription
  card, source+model under an "Advanced" disclosure. No `BlurtConfig` change. UIA-verified.
- **Issue 28** — superseded by 27 (closed).
- **Issue 29** — "Start Blurt when Windows starts": `WindowsStartup` over the HKCU
  Run key, Settings "Startup" checkbox. UIA-verified (write/reflect/remove).
- **Release packaging** — portable build + version 0.1.0 + README + CLAUDE.md update.

## Portable build (the shipping recipe)
**Do NOT use true single-file** (`IncludeNativeLibrariesForSelfExtract=true`): it
makes one .exe but **breaks native whisper loading** → Pur/local transcription
fails. Verified. The working recipe (also in CLAUDE.md/README):

```powershell
$env:DOTNET_ROOT = "$env:USERPROFILE\.dotnet"
& "$env:USERPROFILE\.dotnet\dotnet.exe" publish src/Blurt.App/Blurt.App.csproj `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=false `
    -p:EnableCompressionInSingleFile=true -p:DebugType=none -o publish/Blurt-Portable
```
→ `Blurt.exe` (~65 MB) + 5 WPF native DLLs + `runtimes/` = 18 files. Self-contained
(no .NET needed). Release zip already built: **`publish/Blurt-0.1.0-win-x64.zip`**
(a `Blurt/` folder + `START-HERE.txt`, ~65 MB). `Blurt.exe --selftest` writes
PASS/FAIL/SKIP to `%TEMP%\blurt-selftest.txt` (native-whisper smoke test only —
does NOT test real dictation or network).

## OPEN BUG — issue 30 (found live by user)
**Full Cloud + no internet = dictation lost, no fallback.** Confirmed in code:
`TranscriberResolver` does Online with **no online→local fallback**; only
*refinement* falls back offline, transcription doesn't. Plus the configured local
model (large-v3-turbo) isn't installed (only `small` is), so a local attempt would
need an impossible offline download. See `.scratch/blurt/issues/30-*.md`. Needs a
triage decision (fall back to any installed local model? which? notice?).

## Pending / next steps
1. **Fresh-machine first-run test** (user chose: real download, no pre-seed). Method
   (no admin, no data loss): move `%APPDATA%\Blurt` aside → run
   `publish/Blurt-Portable/Blurt.exe` → exercise onboarding, model download,
   real dictation (cloud + Pur), autostart toggle → then **restore** the folder.
   Their real config (Full cloud, OpenAI key, small model) must come back intact.
2. **Issue 30** — decide & implement the offline fallback.
3. **Release** — only on explicit go: install `gh` (or use web), `git push origin
   main`, tag `v0.1.0`, attach the zip.

## Gotchas / facts
- **Debug build is framework-dependent** → launch it with `DOTNET_ROOT` set, else
  you get a ".NET missing" dialog (that dialog's window is titled `Blurt.exe`). The
  portable/self-contained build needs no `DOTNET_ROOT`.
- **UIA window-finding**: the process has multiple top-level windows (tray host +
  settings, both may report name `Blurt.exe`). Pick the settings window by content
  (the one with a `SaveButton` descendant), not by name/first-child.
- **Screenshots** (`.scratch/blurt/screenshots/capture.ps1 -ProcessId <pid> -Out`)
  go blank when the display is off/locked — fall back to UIA reads.
- **PowerShell is 5.1**: no `??`, no `&&`. `Remove-Item` tripped a sandbox guard a
  couple times — use fresh dir names / `-Force` overwrite instead of deleting.
- User's real config: Transcription **Online**, refinement **OpenAi/gpt-4o-mini**,
  WhisperModel **large-v3-turbo** (NOT on disk), custom prompt = Tigrinya→German.
  API key stored (DPAPI, `apikey.dat`). Autostart Run key currently **not** set
  (test cleaned up).

## ⚠️ Two READMEs — reconcile before publishing (analysed at session end)
There are **two READMEs**:
- **`C:/Users/hagis/dev/blurt-readme`** (worktree, branch `docs/readme`) — an
  **uncommitted** polished public README (9.5 KB) **+ an uncommitted MIT `LICENSE`
  (© 2026 SLM Solutions)**. This is the canonical public README (credits/Blitztext,
  architecture diagram, tech stack, roadmap). **Keep this as the base.**
- **`main`** `README.md` (my commit `edecab1`) — leaner/functional. **Drop or merge**,
  don't ship two. (Also: `main` has no LICENSE; the worktree does.)

The worktree README predates today's findings and has **stale/wrong** bits to fix
before publishing:
1. **Build command** — it shows the old folder publish (`-o publish/Blurt`) and bare
   `dotnet`. Replace with the single-file portable recipe (see "Portable build"
   above) + the `$env:USERPROFILE\.dotnet` SDK note. True single-file breaks whisper.
2. **Model sizes** — it says `small ≈ 460 MB` and a `base ≈ 140 MB` fallback. `base`
   was removed (issue 18); options are **small** (default, ~182 MB q5_1) and
   **large-v3-turbo**. Fix the numbers/names.
3. **Autostart (issue 29)** — missing; add a "Start with Windows" note.
4. **`--selftest`** — optional, worth a mention.
5. **"Fail-soft … falls back to raw text"** — tighten: per **issue 30**, Online
   transcription has NO local fallback, so Full Cloud offline = lost dictation. Don't
   imply graceful offline transcription.

Path-scrub policy (memory: open-source-prep): the remote was scrubbed of machine
paths/usernames; never push the backup-pre-scrub. My committed files use
`%APPDATA%`/`$env:USERPROFILE` placeholders (no literal username) — still re-check
against the policy before any push.

## User preferences / decisions (this session)
- Distribution: **Portable 18-file** layout (Blurt.exe + dlls + runtimes), zipped
  with a `Blurt/` folder. True single-file rejected.
- Autostart shipped **in v0.1.0**.
- Privacy tier UI lives **in the Transcription card** (not its own card).
- **Keep the repo private / don't publish** until the user explicitly releases the code.
- Speed/streaming (chunked transcription, live insert): **deferred** until after
  real cloud-refinement use.
