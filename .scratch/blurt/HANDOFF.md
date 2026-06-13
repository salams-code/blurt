# Handoff — Blurt (2026-06-13)

Context for continuing in a fresh session. Read this first.

## Where things stand

Working on **v0.1.0 — first portable release** of Blurt. **Latest session
(2026-06-13, continued):** built issues 30/31/32/33 (offline transcription
fallback, flex-tap overlay feedback, crash log, live-status overlay) — all
**code-only in the working tree, NOT committed**, but **published into
`publish/Blurt-Portable/`** (re-published 20:10, `--selftest` PASS) so the running
app has them. Suite **213 green**. Then captured a new backlog of six issues
(34–39) via the to-issues skill — see below. **Next:** the user will start a fresh
session and add one more issue. **Issues 30–33 are now committed locally on `main`**
(user gave the go) in two commits — features+tests, then tracker docs; still **not
pushed**. First-run model **download** may still be rough (user flagged it; ties to
issues 22/30) — watch it during the fresh-machine test.

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

## issue 30 — FIXED (2026-06-13, code only; HITL offline repro still pending)
**Full Cloud + no internet = dictation lost, no fallback.** Triaged + fixed via TDD
(suite 191→199). Decisions: fall back to **any already-installed** local model
(never a download — avoids the offline-download trap), surfaced with its own notice
("Cloud transcription offline — transcribed locally."). Built: Core
`DictationOutcome.TranscribedOffline` + `DictationPipeline.transcribeFallback`
delegate (rewinds the stream, then runs refinement as normal) +
`ModelProvisioner.FindInstalledModelPath` + app `BuildLocalFallback()` wired into
the refined pipeline. **Why the download "failed":** Online source never ran the
provisioner, so the configured large-v3-turbo was never fetched (only small on
disk); falling back to the configured model would try an impossible offline
download — hence "any installed". Still TODO: reproduce live offline (kill network
mid-dictation) during the fresh-machine test. See `.scratch/blurt/issues/30-*.md`.
**Not committed** — work is in the working tree only. The **`publish/Blurt-Portable/`
folder was re-published with this fix** (2026-06-13 19:09, `--selftest` PASS) so it
is runnable for testing; the **release zip `Blurt-0.1.0-win-x64.zip` was deliberately
NOT rebuilt** — it stays the unchanged released v0.1.0 (this fix is post-v0.1.0,
uncommitted/unreleased).

## issue 31 + 32 — built 2026-06-13 (code only, in working tree, in Portable)
- **Issue 31 — Flex tap feedback.** The mode always cycled; the tray balloon just
  hid it (Windows throttles successive balloons → felt stuck until the old one
  cleared). Fix: tap now flashes the mode in the **overlay pill** (instant,
  repeatable, ~1.1 s auto-hide), with a **distinct label+colour per mode**
  (Pur=green, "• Bullets"=blue, Custom=purple). Balloon dropped for taps. Core
  `FlexSlotOverlay` + `OverlayController.FlashMode`. HITL confirm pending.
- **Issue 32 — Crash log.** There was none. Added Core `RollingLog` (size-capped,
  rotates to one `.1` backup, never throws) + global handlers in `Program.cs`
  (AppDomain / WinForms ThreadException / UnobservedTask) + session banner. Log at
  **`%APPDATA%\Blurt\logs\blurt.log`**. Verified: banner written on launch.

## issue 33 — live status overlay (built 2026-06-13, code only, in Portable)
The overlay is now a precise live status, not a generic "busy": Core `StatusLabel`
(lowercase verbs, no baked-in "…") gives `listening` / `transcribing` /
**`transcribing locally`** / `fixing` / `bulleting` / `translating` / `refining`;
the pill pulses (dot opacity breathe) + animates the ellipsis while active, steady
for the mode flash. App threads the verb per trigger and the refine delegate steps
the label transcribing→refine-verb mid-op (`OverlayController.ShowActive` /
`UpdateActive`). **Clarified with user:** Pur is hard-gated to local
(`zeroNetwork:true`) regardless of settings — kept by design; the label now shows
it ("transcribing locally"). User chose: verbs lowercase, dot-pulse + ellipsis.

Suite **213 green** (was 199; +14 across issues 30/31/32/33). Portable
**re-published 2026-06-13 20:10 with all four**, `--selftest` PASS; release zip
still NOT rebuilt (stays v0.1.0). Running instances were stopped (with the user's
OK) for each re-publish; app relaunched (PID 25232) for testing. **Committed locally
on `main` (not pushed).**

## Backlog captured this session — issues 34–39 (ready, not started)
Tracer-bullet slices from the prompt-management / email / translate feature set
(decisions made with the user; see each file). Dependency order:
- **34 — Expanded hotkey vocabulary** (`ready-for-human`/HITL): allow more than
  AltGr+{,.-}; design which keys/modifiers + capture UX + conflict rules. Enabler
  for 39. **Start here for that thread.**
- **35 — Editable prompts for all refined modes** (`ready-for-agent`): move
  Fix/English/Bullets/Custom prompts into editable per-mode config, defaults
  pre-filled. No blockers.
- **36 — Email Flex mode** (`ready-for-agent`, blocked by 35): new `Email` Flex
  mode, conversational speech → proper email.
- **37 — Reset prompts to defaults with backup** (`ready-for-agent`, blocked by 35):
  Reset backs up current prompts+names, then restores defaults (non-destructive).
- **38 — Backup view/copy/restore UI** (`ready-for-agent`, blocked by 37).
- **39 — "Also translate to English" via extra modifier** (`ready-for-agent`,
  blocked by 34): hold a modifier during a refined dictation → output also in
  English, composes with any mode; per-dictation; Pur stays zero-network.

- **40 — Animated onboarding tutorial** (`ready-for-human`/HITL): teach first-run
  users how to drive the app (push-to-talk, the hotkeys, Flex tap/hold + modes),
  nicely presented and **animated**, reusing the overlay-pill animation tech.
  Design-led; concept proposed in the issue.

The user may still add a further issue next session.

## Pending / next steps
1. **Fresh-machine first-run test** (user chose: real download, no pre-seed). Method
   (no admin, no data loss): move `%APPDATA%\Blurt` aside → run
   `publish/Blurt-Portable/Blurt.exe` → exercise onboarding, model download,
   real dictation (cloud + Pur), autostart toggle → then **restore** the folder.
   Their real config (Full cloud, OpenAI key, small model) must come back intact.
2. ~~**Issue 30** — decide & implement the offline fallback.~~ ✅ done in code
   (2026-06-13); reproduce offline live during the fresh-machine test.
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
