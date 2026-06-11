using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Blurt.Core;

namespace Blurt.App;

/// <summary>
/// The borderless, click-through, always-on-top WPF "pill" that shows the
/// dictation status (design contract §9). Lives in the WinForms STA process and
/// is pumped by the WinForms message loop — no second
/// <see cref="System.Windows.Application"/> is created. It never activates and
/// never receives clicks: extended window styles make every click fall through to
/// the app underneath and stop it stealing the target app's focus, which is the
/// whole point — the user keeps typing into their editor while the pill floats
/// above it.
/// </summary>
internal partial class OverlayWindow : Window
{
    // Extended window-style plumbing. WS_EX_TRANSPARENT makes the window
    // click-through (hit-tests fall to the window beneath); WS_EX_NOACTIVATE stops
    // it ever taking focus; WS_EX_TOOLWINDOW keeps it out of Alt-Tab.
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    public OverlayWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Set the pill's status dot colour and label from Core's pure mappings. The
    /// dot reuses <see cref="TrayPalette"/> (red = listening/recording, amber =
    /// transcribing/processing) so the overlay and tray speak one colour language.
    /// </summary>
    public void SetState(OverlayState state)
    {
        StatusText.Text = OverlayText.For(state);

        var trayState = state == OverlayState.Transcribing
            ? TrayState.Processing
            : TrayState.Recording;
        var (r, g, b) = TrayPalette.For(trayState);
        // Fully-qualified: System.Drawing.Color (WinForms implicit using) and
        // System.Windows.Media.Color both in scope here.
        StatusDot.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));
    }

    // Apply the click-through / no-activate / tool-window styles as soon as the
    // HWND exists. Done here (not in the ctor) because the handle is only created
    // on SourceInitialized.
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var hwnd = new WindowInteropHelper(this).Handle;
        var exStyle = GetWindowLong(hwnd, GwlExStyle);
        SetWindowLong(
            hwnd, GwlExStyle,
            exStyle | WsExTransparent | WsExNoActivate | WsExToolWindow);
    }

    /// <summary>
    /// Convert a placement computed in physical screen pixels to the device-
    /// independent units WPF's <see cref="Window.Left"/>/<see cref="Window.Top"/>
    /// expect, so the pill lands where intended at any DPI scaling.
    /// </summary>
    public void MoveToDevicePixels(double pixelX, double pixelY)
    {
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget is { } target)
        {
            var dip = target.TransformFromDevice.Transform(new System.Windows.Point(pixelX, pixelY));
            Left = dip.X;
            Top = dip.Y;
        }
        else
        {
            // No HWND/source yet (before first Show): physical ≈ DIP at 100% — the
            // first Show()'s placement corrects it once the source exists.
            Left = pixelX;
            Top = pixelY;
        }
    }
}
