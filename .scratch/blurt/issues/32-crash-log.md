# 32 — No crash log: a crash leaves nothing to diagnose

Status: done (built via TDD, 2026-06-13)

## Parent

[.scratch/blurt/PRD.md](../PRD.md)

## What the user asked (2026-06-13)

While testing the portable: is there a log file that cleans itself up so it can't
grow too large, and can it be read back after a crash?

## State before

None. No logging and no global exception handler — `Program.Main` went straight
into the tray context. A crash left nothing on disk (only an unreliable Windows
Event Log / WER entry). The only file the app wrote was `%TEMP%\blurt-selftest.txt`.

## What was built

- **Core `RollingLog`** ([RollingLog.cs](../../../src/Blurt.Core/RollingLog.cs)) —
  append-only, timestamped, **size-capped** (rotates to a single `.1` backup at
  ~512 KB, so ≤ ~2× that ever on disk). `Write` locks and **never throws** (it runs
  inside crash handlers, where a throw would mask the original fault). 4 tests
  (append+mkdir, rotation, single-backup, never-throws).
- **Global crash capture** ([Program.cs](../../../src/Blurt.App/Program.cs)) —
  wires the log to `AppDomain.UnhandledException`, WinForms `ThreadException`
  (CatchException mode), and `TaskScheduler.UnobservedTaskException` (the app's
  fire-and-forget dictation tasks), plus a try/catch around `Application.Run`. Logs
  a session banner (`=== Blurt <version> started (pid …) ===`) on startup.

## Where the log lives

`%APPDATA%\Blurt\logs\blurt.log` (+ `blurt.log.1` backup) — next to the models
folder. Verified live: launching the portable writes the session banner.

## Acceptance criteria

- [x] A log file is written and self-rotates (bounded size).
- [x] Unhandled exceptions from any of the three channels are recorded.
- [x] Logging never throws / never becomes a second crash.
- [x] Suite stays green (210).

## Notes

WinForms `CatchException` means a UI-thread fault is logged and the app keeps
running (fail-soft, design §10) rather than dying. If a hard crash is ever needed
for repro, the AppDomain handler still records terminating exceptions.
