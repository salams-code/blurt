# 06 — Status overlay + tray state feedback

Status: done (verify-sweep 2026-06-12)
Type: HITL

## Parent

[.scratch/blurt/PRD.md](../PRD.md)

## What to build

Visual feedback wired to the Pur dictation flow. A small borderless, top-most,
click-through WPF overlay pill anchored to the mouse pointer (or a fixed
bottom-center anchor) that shows "listening…" while recording and "transcribing…"
while processing, then disappears on insert. The tray icon changes
colour/animation in sync (idle → recording → processing). An optional start/stop
sound, off by default.

## Acceptance criteria

- [x] While holding the key the overlay shows a "listening" state near the pointer/anchor.
- [x] After release the overlay shows "transcribing", then disappears once text is inserted.
- [x] The overlay is click-through and never steals focus from the target app.
- [x] The tray icon visibly reflects idle / recording / processing.
- [x] Sound is off by default and can be toggled on.

## Blocked by

- 05 — Pur dictation end-to-end

## Implementation note (handoff)

### Core (pure, TDD — net8.0, no WPF)

All status/placement decisions live in `src/Blurt.Core/Overlay.cs` and are unit-tested:

- `enum OverlayState { Hidden, Listening, Transcribing }`.
- `static string OverlayText.For(OverlayState)` → "listening…" / "transcribing…" / "" (Hidden).
- Value types `readonly record struct OverlayPoint(double X, double Y)`,
  `OverlaySize(double Width, double Height)`,
  `OverlayBounds(double X, double Y, double Width, double Height)`.
- `static OverlayPoint OverlayPlacement.Resolve(OverlayAnchor anchor, OverlayPoint mouse, OverlaySize overlay, OverlayBounds screen)`
  — MousePointer = +16/+16 off the cursor, clamped into screen bounds; BottomCenter = horizontally
  centred in the screen, fixed margin above the bottom. Always clamped so the pill never spills off-screen.
- `enum TrayState { Idle, Recording, Processing }` + `static (byte R, byte G, byte B) TrayPalette.For(TrayState)`
  (idle = neutral grey, recording = red, processing = amber). The overlay's status dot reuses the same palette.

14 new tests (3 OverlayText, 7 OverlayPlacement incl. offset / all four clamp edges / BottomCenter centring on
origin and offset screens, 4 TrayPalette). Full Core suite **91 passed / 0 failed**. `build src/Blurt.App` is **green (0 warnings/0 errors)**.

### App (WPF/WinForms — compiled, manual verification pending)

- `src/Blurt.App/Blurt.App.csproj`: added `<UseWPF>true</UseWPF>` (coexists with WinForms on the one STA thread).
  Enabling WPF narrows the WinForms implicit-usings to their intersection and drops `System.IO`, so a
  `<Using Include="System.IO" />` global-using was re-added (the audio/transcriber adapters need Stream/MemoryStream).
- `OverlayWindow.xaml` / `.xaml.cs` (new): borderless, transparent, top-most, `ShowActivated=False`,
  `IsHitTestVisible=False`. The rounded pill (CornerRadius 14, `#CC1E1E1E`, drop shadow) has a coloured status
  `Ellipse` + white `TextBlock`. **Click-through + never-activate** is enforced in `OnSourceInitialized` by OR-ing
  the extended styles `WS_EX_TRANSPARENT (0x20)`, `WS_EX_NOACTIVATE (0x08000000)`, `WS_EX_TOOLWINDOW (0x80)` via
  `GetWindowLong`/`SetWindowLong` P/Invoke. `MoveToDevicePixels` converts physical-pixel placement to WPF DIPs via
  `CompositionTarget.TransformFromDevice`.
- `OverlayController.cs` (new): holds one lazily-created `OverlayWindow`; `Show(OverlayState)`/`Hide()` set the dot+text,
  position via `OverlayPlacement.Resolve` (fed `Cursor.Position` and `Screen.FromPoint(...).Bounds`) and `Show()`/`Hide()`
  on the WinForms STA thread (no second `System.Windows.Application`). `Hidden` → hide.
- `TrayIcons.cs` (new): builds idle/recording/processing icons programmatically from `TrayPalette.For(...)` — a filled
  16×16 dot via `Icon.FromHandle(bitmap.GetHicon())`, freeing each HICON with `DestroyIcon` on dispose.
- `TrayApplicationContext.cs` (wired): added `OverlayController _overlay`, `TrayIcons _trayIcons`, `bool _soundEnabled`
  (all read from config at start-up); tray now starts on the idle icon. Helpers `SetTrayState`, `EnterRecording`,
  `EnterProcessing`, `ReturnToIdle`, `PlaySound`. Lifecycle:
  - Down branches (English/Fix/Flex, after `TryStartRecording`): `EnterRecording()` (listening pill + red tray + optional start beep).
  - `DictateAsync`/`RefineAndInjectAsync` start (after `_recorder.Stop()`): `EnterProcessing()` (transcribing pill + amber tray);
    their `finally`: `ReturnToIdle()` (pill hidden + idle tray) — so the pill disappears once the text is inserted (or on failure).
  - Flex-slot **tap** (cycle, no dictation): `ReturnToIdle()` immediately, no "transcribing" pill.
  - Stale key-up branches also `ReturnToIdle()` so the pill/tray can't get stuck.
  - `Dispose` closes the overlay window and frees the tray-icon GDI handles.
- **Sound**: optional, default off (`config.SoundEnabled`). When on, a short `SystemSounds` beep on record start/stop; otherwise silent.

### Known DPI nuance (v1)

Placement is computed in physical pixels (Core) and converted to DIPs in `MoveToDevicePixels`. At >100% scaling the
conversion uses the source's device transform; if the overlay first shows before its HwndSource exists the fallback
treats physical ≈ DIP (correct at 100%, and self-corrects on the first positioned `Show`). Pixel-exact placement isn't
required by design (the pill only needs to sit near the cursor), so a small offset at high scaling is an accepted v1 nuance.

### Manual checks (pending on a machine with a GUI/mic)

- [ ] Hold a trigger → a red "listening" pill appears near the mouse pointer; the target app keeps focus (keep typing while held).
- [ ] Release → pill switches to "transcribing"; it disappears once the text is inserted.
- [ ] A click "through" the overlay lands in the app underneath (click-through).
- [ ] Tray icon visibly shows idle (grey) / recording (red) / processing (amber).
- [ ] Flex-slot tap cycles the mode without ever showing the "transcribing" pill.
- [ ] With `SoundEnabled` toggled on, a short beep plays on record start/stop; off by default = silent.
- [ ] Switch `OverlayAnchor` to `BottomCenter` → pill sits centred above the bottom edge instead of by the cursor.

## Comments

**2026-06-12 (agent, verify-sweep):** Overlay pill renders correctly with the issue-19 theme (screenshot 19-overlay-dark-*.png); click-through/no-activate styles set in OnSourceInitialized; tray icons driven by TrayPalette (unit-tested). Exercised live in the user's 2026-06-12 HITL session (daily dictation use); the only finding was flex-tap latency, split into issue 21 and fixed.
