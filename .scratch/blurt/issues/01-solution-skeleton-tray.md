# 01 — Solution skeleton + tray that runs

Status: ready-for-human
Type: HITL

## Parent

[.scratch/blurt/PRD.md](../PRD.md)

## What to build

The walking skeleton: a .NET 8 solution that launches as a tray application and
quits cleanly. Establish the project layout the rest of the work plugs into — an
app project (tray/host), a core library (testable logic), and a test project —
plus a portable build that runs `Blurt.exe` from a user folder with no installer
and no admin rights. The tray shows an idle icon with a context menu that can
exit the app.

## Acceptance criteria

- [ ] Solution builds with `dotnet build` on Windows (.NET 8) into three projects: app, core lib, tests.
- [ ] Running the app shows a tray icon with at least an "Exit" menu item that terminates the process.
- [ ] The app runs from a copied folder without installation or admin elevation.
- [ ] The test project runs (even with a single trivial passing test) via `dotnet test`.
- [ ] No source from the macOS `blitztext-app` is reused.

## Blocked by

None - can start immediately.
