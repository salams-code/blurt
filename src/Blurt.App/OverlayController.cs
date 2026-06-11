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
    private readonly OverlayAnchor _anchor;
    private OverlayWindow? _window;

    public OverlayController(OverlayAnchor anchor) => _anchor = anchor;

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

        var window = _window ??= new OverlayWindow();
        window.SetState(state);

        // Make it visible (without stealing focus) before positioning so its
        // HwndSource exists for the DPI transform; ShowActivated=False keeps focus
        // with the target app.
        window.Show();
        Reposition(window);
    }

    /// <summary>Hide the pill if it is showing. Idempotent.</summary>
    public void Hide() => _window?.Hide();

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
        _window?.Close();
        _window = null;
    }
}
