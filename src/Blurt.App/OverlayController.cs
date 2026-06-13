using System.Windows.Forms;
using Blurt.Core;

namespace Blurt.App;

/// <summary>
/// Owns the single <see cref="OverlayWindow"/> and drives it from
/// <see cref="OverlayState"/>: <see cref="Show"/> positions the pill (via Core's
/// pure <see cref="OverlayPlacement"/>) and makes it visible without activating
/// it; <see cref="Hide"/> hides it. The window is created lazily on first show so
/// nothing renders until the first dictation. All access is from the WinForms STA
/// UI thread — the lifecycle calls in <see cref="TrayApplicationContext"/> already
/// run there — so no marshalling is needed.
/// </summary>
internal sealed class OverlayController : IDisposable
{
    // How long a Flex-slot mode flash lingers before auto-hiding. Long enough to
    // read, short enough that the pill doesn't loiter after a tap.
    private static readonly int ModeFlashMs = 1100;

    private readonly OverlayAnchor _anchor;
    private OverlayWindow? _window;

    // Auto-hide for the transient mode flash. Single-shot, lives on the WinForms
    // UI thread (the only thread that touches the overlay), so its Tick runs where
    // window access is legal. Any explicit Show/Hide cancels a pending flash so a
    // started recording is never hidden out from under itself.
    private readonly System.Windows.Forms.Timer _flashTimer = new() { Interval = ModeFlashMs };

    public OverlayController(OverlayAnchor anchor)
    {
        _anchor = anchor;
        _flashTimer.Tick += (_, _) => Hide();
    }

    /// <summary>
    /// Show the pill in <paramref name="state"/>, positioned for the configured
    /// anchor at the current cursor / its screen. Never activates the window, so
    /// the target app keeps focus.
    /// </summary>
    public void Show(OverlayState state)
    {
        if (state == OverlayState.Hidden)
        {
            Hide();
            return;
        }

        _flashTimer.Stop();   // a real state supersedes any pending mode flash
        var window = _window ??= new OverlayWindow();
        window.SetState(state);

        // Make it visible (without stealing focus) before positioning so its
        // HwndSource exists for the DPI transform; ShowActivated=False keeps focus
        // with the target app.
        window.Show();
        Reposition(window);
    }

    /// <summary>
    /// Show a live activity in the pill — <paramref name="label"/> from Core's
    /// <see cref="StatusLabel"/>, coloured by <paramref name="colorState"/>
    /// (red while listening, amber while transcribing/refining) — and animate it
    /// (pulse + ellipsis). Positions the pill; use <see cref="UpdateActive"/> to
    /// change the label mid-operation without moving it.
    /// </summary>
    public void ShowActive(string label, OverlayState colorState)
    {
        _flashTimer.Stop();
        var window = _window ??= new OverlayWindow();
        var (r, g, b) = DotFor(colorState);
        window.SetActive(label, r, g, b);
        window.Show();
        Reposition(window);
    }

    /// <summary>
    /// Change the live label/colour in place (e.g. transcribing → fixing) without
    /// re-positioning, so the pill stays put as the operation moves through its
    /// phases. No-op if the pill was never shown.
    /// </summary>
    public void UpdateActive(string label, OverlayState colorState)
    {
        if (_window is not { } window)
        {
            return;
        }

        var (r, g, b) = DotFor(colorState);
        window.SetActive(label, r, g, b);
    }

    // Red while listening (recording), amber while transcribing/refining — the
    // same colour language as the tray (TrayPalette).
    private static (byte R, byte G, byte B) DotFor(OverlayState colorState)
    {
        var trayState = colorState == OverlayState.Transcribing
            ? TrayState.Processing
            : TrayState.Recording;
        return TrayPalette.For(trayState);
    }

    /// <summary>
    /// Flash the Flex-slot <paramref name="mode"/> in the pill and auto-hide after
    /// a moment. Instant and repeatable — a rapid second tap just re-shows with the
    /// new mode and restarts the timer — unlike the tray balloon Windows throttles.
    /// </summary>
    public void FlashMode(FlexSlotMode mode)
    {
        var window = _window ??= new OverlayWindow();
        var (r, g, b) = FlexSlotOverlay.Dot(mode);
        window.SetModeFlash(FlexSlotOverlay.Label(mode), r, g, b);
        window.Show();
        Reposition(window);

        // Restart the single-shot timer so back-to-back taps keep the pill up,
        // showing the latest mode, and it hides once tapping stops.
        _flashTimer.Stop();
        _flashTimer.Start();
    }

    /// <summary>Hide the pill if it is showing. Idempotent.</summary>
    public void Hide()
    {
        _flashTimer.Stop();
        _window?.StopAnimations();   // don't leave the pulse/ellipsis ticking off-screen
        _window?.Hide();
    }

    // Resolve the pill's top-left in physical pixels from the live cursor position
    // and the screen it sits on, then move the window there (converted to DIPs).
    private void Reposition(OverlayWindow window)
    {
        var cursor = Cursor.Position;
        var screen = Screen.FromPoint(cursor).Bounds;

        var point = OverlayPlacement.Resolve(
            _anchor,
            new OverlayPoint(cursor.X, cursor.Y),
            new OverlaySize(window.ActualWidth, window.ActualHeight),
            new OverlayBounds(screen.X, screen.Y, screen.Width, screen.Height));

        window.MoveToDevicePixels(point.X, point.Y);
    }

    public void Dispose()
    {
        _flashTimer.Dispose();
        _window?.Close();
        _window = null;
    }
}
