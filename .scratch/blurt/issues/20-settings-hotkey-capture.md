# 20 — Configuration windows must accept input (modeless WPF interop + suspend hook + hotkey capture)

Status: done (HITL-verified 2026-06-12; close-crash regression fixed under issue 23)
Type: AFK App-interop + AFK Core (hotkey capture/validation unit-tested) / HITL UI check

## Parent

[.scratch/blurt/PRD.md](../PRD.md)

## What's broken (HITL findings, 2026-06-12)

In the **Settings** window the user can't type into the text fields (Base URL,
refinement model, hotkeys): existing text can be deleted but no new characters
can be entered. As a knock-on, **Save doesn't persist** (a cleared-but-not-retyped
Base URL fails validation, so the save is rejected — the error panel sits at the
bottom of a scroll and is easy to miss). Hotkey chords can't be entered at all.

## Root cause (code-confirmed)

1. **Modeless WPF keyboard interop missing.** The Settings window is shown
   modelessly — `window.Show()` in `TrayApplicationContext.OpenSettings`
   ([TrayApplicationContext.cs](../../../src/Blurt.App/TrayApplicationContext.cs)) —
   from the WinForms message loop (`Application.Run` in `Program.cs`), **without**
   `System.Windows.Forms.Integration.ElementHost.EnableModelessKeyboardInterop(window)`.
   Without that call, a modeless WPF window hosted in a WinForms loop doesn't get
   keyboard input routed to it. (Onboarding works because it's **modal** —
   `ShowDialog()` runs WPF's own dispatcher loop.)
2. **Global trigger hook stays live while configuring.** The `WH_KEYBOARD_LL`
   hook ([KeyboardHook.cs](../../../src/Blurt.App/KeyboardHook.cs)) isn't suspended
   while a config window is open, so the trigger characters `, . -` (and AltGr
   chords) get swallowed and can fire a dictation behind the window.

## What to build

- **Enable modeless keyboard interop** for the Settings window
  (`ElementHost.EnableModelessKeyboardInterop`) so its text fields accept input.
  **Keep it modeless** — do not switch to `ShowDialog()` (the tray must stay
  responsive; single-instance `Activate()` must still work).
- **Suspend the global trigger hook while a configuration window is open/focused**
  and restore it when it closes, so trigger characters can be typed and no
  dictation fires behind the window. (Onboarding runs before the hook is installed,
  so it's already safe; the guard is mainly for Settings.)
- **Hotkey fields: press-to-capture.** Focus a hotkey field, press the chord
  (e.g. AltGr+,) and it's captured and shown as `AltGr+,`. The capture/validation
  decision (which (modifier, key) combos are valid triggers, → chord string) lives
  in `Blurt.Core` and is unit-tested (building on `HotkeyBinding`); the WPF key
  handling is the thin shell. Manual text entry of the chord must also still work.
- **Make a rejected Save obvious** — when validation fails, surface the error panel
  (scroll it into view / focus it) so the user sees why nothing was saved.

## Acceptance criteria

- [ ] In Settings, every text field (Base URL, refinement model, hotkeys) accepts typed input.
- [ ] While a config window is open, pressing a trigger chord does not start a dictation behind it.
- [ ] A hotkey field can be set by focusing it and pressing the chord; manual text entry still works too; invalid chords are rejected with guidance.
- [ ] A save rejected by validation visibly tells the user why (the error panel is brought into view).
- [ ] Hotkey capture/validation logic is unit-tested in `Blurt.Core`; the suite stays green; the app builds.

## Blocked by

- None. Best done before 19 (visual polish) so the new hotkey control is styled once.

## Comments

### HITL test 2026-06-12 (agent-guided)

The four happy-path criteria were verified live in the running self-contained
build:

- ✅ Text fields accept typed input (real keyboard — the original swallow bug is gone).
- ✅ Hotkey field captures a pressed chord (`AltGr+,`).
- ✅ A trigger chord pressed with Settings focused does not fire a dictation behind it.
- ✅ A rejected save surfaces the **specific** validation message (e.g. the hotkey-conflict
  text naming the two triggers and the shared key) in the error panel, brought into view.

A regression was found during this check: clicking **Cancel** (and a successful
**Save**) crashed the whole app — the modeless conversion left `OnSave`/`OnCancel`
setting `DialogResult`, which throws on a `Show()`-modeless window. Tracked and
**fixed under issue 23**, then re-verified live (Save and Cancel both keep the app
running and Save persists). With that resolved, all of issue 20's acceptance
criteria are met — marked done.
