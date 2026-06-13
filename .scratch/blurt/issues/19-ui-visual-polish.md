# 19 — Visual polish across overlay, settings, and onboarding

Status: done (HITL-approved 2026-06-13)
Type: HITL

## Parent

[.scratch/blurt/PRD.md](../PRD.md)

## What to build

A visual pass over the WPF surface — the status overlay pill, the settings
window, and the onboarding wizard — for a cleaner, more modern look. Establish a
small, consistent visual style (spacing, typography, colours, rounded surfaces,
subtle states) and apply it across all three. WPF supports full custom styling,
so this is purely a design/appearance effort; behaviour is unchanged.

Best done **after** the provider (17) and model (18) UI changes land, so the new
controls are styled once rather than restyled.

## Acceptance criteria

- [x] The overlay pill, settings window, and onboarding wizard share a consistent, modern visual style.
- [x] No behavioural regressions — all existing flows still work and the test suite stays green.
- [x] The look is reviewed and approved by the user (HITL).

## Blocked by

- 17 — Switchable refinement provider with persistent key
- 18 — Use the selected Whisper model + per-selection download guidance

## Design decisions (user)

- **Lightweight, hand-rolled WPF styles** — no new NuGet dependencies (keep it
  clean and safe to audit).
- **Theme follows the system**: Windows 11 light/dark is read from
  `AppsUseLightTheme` and tracked live (`SystemEvents.UserPreferenceChanged`),
  including the title bar (`DWMWA_USE_IMMERSIVE_DARK_MODE`).
- Settings window is now **resizable** (default 580×700, min 480×440) with the
  value column stretching and a footer (errors + Save/Cancel) pinned below the
  scroll area.
- Accent = Windows 11 default blue (`#0067C0` light / `#4CC2FF` dark), used for
  the primary action per surface (Save / Next / Download), focus borders,
  checked states, progress and the onboarding level bar.

## Implementation notes

- `ThemeManager.cs` — semantic brush palette (light/dark), one shared
  `ResourceDictionary` merged per window (no `App.xaml` in this WinForms-hosted
  process); palette swap restyles open windows live via DynamicResource.
- `Themes/Controls.xaml` — flat, rounded ControlTemplates for TextBox,
  PasswordBox, ComboBox (+items/popup), Button (+`AccentButton`), CheckBox,
  RadioButton, ProgressBar (pulse when indeterminate), slim ScrollBar; shared
  `Card`/`GroupHeader`/`FieldLabel`/`Hint`/`BodyText` styles.
- Overlay pill is theme-aware (light capsule on light, dark on dark) and keeps
  the TrayPalette status-dot colours.
- Dev affordance: `Blurt.exe --settings | --onboarding | --overlay` opens that
  surface directly for visual checks/screenshots.

## Verification (screenshots in `.scratch/blurt/screenshots/`)

- `19-settings-dark.png` — dark theme, default size
- `19-settings-dark-wide.png` — resized: value column stretches, footer pinned
- `19-settings-light-wide.png` — live system-theme switch to light while open
- `19-onboarding-dark.png` — onboarding step 1, same card/control language
- `19-overlay-dark-*.png` — status pill
- Core test suite green (163/163) after the change.
