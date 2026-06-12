# 23 — Settings window crashes the app on Save/Cancel (DialogResult on a modeless window)

Status: done (fixed + HITL-verified, 2026-06-12)
Type: AFK App-interop bugfix / HITL UI check

## Parent

[.scratch/blurt/PRD.md](../PRD.md)

## What's broken (HITL finding, 2026-06-12)

Clicking **Cancel** in the Settings window terminates the whole tray app instead
of just closing the window. A successful **Save** has the same fault. The app
must keep running in the tray after either action.

## Root cause (code-confirmed)

Issue 20 made the Settings window **modeless** (`window.Show()` in
`TrayApplicationContext.OpenSettings`,
[TrayApplicationContext.cs](../../../src/Blurt.App/TrayApplicationContext.cs)) and
added `ElementHost.EnableModelessKeyboardInterop` to fix text input — correct so
far. But `OnSave` and `OnCancel`
([SettingsWindow.xaml.cs](../../../src/Blurt.App/SettingsWindow.xaml.cs)) still set
`window.DialogResult` (`true` / `false`). In WPF, setting `DialogResult` on a
window shown with `Show()` (not `ShowDialog()`) throws `InvalidOperationException`.
The exception is unhandled in the Button.Click handler → the process crashes.

Two consequences:

1. **Cancel** → `DialogResult = false` throws → app exits. (User's symptom.)
2. **Save** → `DialogResult = true` throws → app exits before the change is
   applied.
3. Latent even without the crash: the `Closed` handler gates on
   `window.DialogResult == true`, which can never be `true` on a modeless window,
   so a save would never be applied.

The Core unit tests can't catch this — it lives entirely in the WPF shell. The
HITL UI check found it.

## What to build

- **Stop using `DialogResult` for the modeless window.** In `OnSave`, set
  `SavedConfig` (already done) and call `Close()`; in `OnCancel`, call `Close()`.
- **Gate apply on `SavedConfig`, not `DialogResult`.** Change the `Closed` handler
  to `if (window.SavedConfig is { } saved)` — `SavedConfig` is set only on a real
  save, so it is the correct signal.
- Keep the window **modeless** (do not regress issue 20's input fix) and keep the
  hook-resume logic intact.
- Confirm by HITL: Save closes the window and applies live (hotkeys/overlay/sound
  take effect, per `ApplySettings`) with no restart; Cancel closes the window; the
  tray app stays alive through both; the single-instance `Activate()` path and a
  re-open still work.

## Acceptance criteria

- [ ] Clicking Cancel closes only the Settings window; the tray app keeps running.
- [ ] A successful Save closes the window, persists the config, applies live changes (no crash, no app restart), and the tray app keeps running.
- [ ] A saved config is actually applied (hotkeys/overlay/sound live; model/source on next launch).
- [ ] The window stays modeless; text input (issue 20) still works; re-opening Settings works.
- [ ] The suite stays green; the app builds.

## Blocked by

- None. Blocks marking issue 20 done (this regression is in issue 20's surface),
  and blocks the save-dependent HITL checks of issues 17 and 18 (both need a
  working Save).

## Comments

### Fix + HITL verification 2026-06-12 (agent-guided)

Fix applied: `OnSave`/`OnCancel` now call `Close()` instead of setting
`DialogResult`; the `Closed` handler in `TrayApplicationContext.OpenSettings`
gates apply on `window.SavedConfig` instead of `DialogResult == true`. 163 Core
tests stay green; self-contained build republished.

Re-verified live in the running build (PID stayed 13652 throughout — no crash, no
restart):

- ✅ Cancel closes only the Settings window; tray app keeps running.
- ✅ Save closes the window, app keeps running, and the change persists live
  (toggled "Play a sound on record start/stop" → `config.json` `SoundEnabled: true`).

Unblocks issue 20 (now done) and the save-dependent HITL checks of issues 17/18.
