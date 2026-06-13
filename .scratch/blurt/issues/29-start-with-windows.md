# 29 — "Start Blurt when Windows starts" toggle

Status: ready-for-human (implemented 2026-06-13, UIA-verified; part of v0.1.0)
Type: App UI (Settings checkbox) + App I/O (per-user Run key) — registry shell, no Core/TDD

## Parent

[.scratch/blurt/PRD.md](../PRD.md)

## Motivation (user, 2026-06-13)

Blurt is a tray app you want available all the time — so it should be able to
launch itself at sign-in without the user re-running the exe each boot. Asked for
alongside the v0.1.0 portable release.

## Approach

A **per-user** auto-start via `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`
— no admin rights, unlike the machine-wide key or Task Scheduler. The registry
entry's presence is the source of truth:

- Settings → **Startup** → "Start Blurt when Windows starts" checkbox.
- On load the checkbox reflects the actual Run-key state (not config), so it stays
  correct even if the user edits startup apps via Windows Settings.
- On save it writes the quoted current exe path (`Environment.ProcessPath`) or
  removes the entry.

Pure registry I/O, so it lives in the App as a thin shell ([WindowsStartup.cs](../../../src/Blurt.App/WindowsStartup.cs)),
manually/UIA-verified like the other I/O shells (mic, hook, injector) — no Core
logic to unit-test. Nothing about it lives in `BlurtConfig`.

Portable caveat (surfaced in the Settings hint and README): the Run entry stores
an absolute path, so moving the Blurt folder breaks it until the user re-toggles.

## Acceptance criteria

- [x] Settings has a "Start Blurt when Windows starts" checkbox under a Startup section.
- [x] Enabling + saving writes the Run key to the current exe; disabling removes it.
- [x] The checkbox reflects the actual registry state on load.
- [x] Per-user (HKCU), no admin required.
- [x] Suite stays green (191); app builds clean.

## Comments

**2026-06-13 (agent) — built + verified.** Implemented `WindowsStartup`
(IsEnabled / SetEnabled over the HKCU Run key) wired into the Settings load/save.
Verified live via UI Automation against the running window: load=Off → toggle on +
Save wrote `…\Blurt.exe` to the Run key → reload reflected On → toggle off + Save
removed it. Registry left clean. Shipped in v0.1.0.

Tangential to this issue but done in the same session: the portable build was
moved to single-file (`PublishSingleFile=true`, natives on disk) and a
`--selftest` native-whisper smoke test added — see CLAUDE.md / README.
