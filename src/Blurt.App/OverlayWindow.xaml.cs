using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
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

    // Animation for the live "working" states (listening/transcribing/refining):
    // the dot breathes (opacity pulse) and the trailing ellipsis cycles, so the
    // pill reads as active rather than frozen. Both run on the WPF dispatcher (the
    // UI thread) and are stopped for the static mode-flash pill.
    private readonly DispatcherTimer _ellipsisTimer =
        new() { Interval = TimeSpan.FromMilliseconds(350) };
    private string _baseLabel = "";
    private int _ellipsisCount;

    private static readonly DoubleAnimation PulseAnimation = new()
    {
        From = 1.0,
        To = 0.3,
        Duration = TimeSpan.FromMilliseconds(750),
        AutoReverse = true,
        RepeatBehavior = RepeatBehavior.Forever,
    };

    public OverlayWindow()
    {
        // Before InitializeComponent so the pill's DynamicResource surface
        // brushes resolve from the shared theme (issue 19).
        ThemeManager.Apply(this);
        InitializeComponent();

        _ellipsisTimer.Tick += (_, _) =>
        {
            // Cycle 1→2→3 dots, padding to a constant width so the pill doesn't
            // resize (and visibly jiggle) on every tick.
            _ellipsisCount = _ellipsisCount % 3 + 1;
            StatusText.Text = _baseLabel + new string('.', _ellipsisCount) + new string(' ', 3 - _ellipsisCount);
        };
    }

    /// <summary>
    /// Show a live activity (<paramref name="label"/> from Core's
    /// <see cref="StatusLabel"/>) with the given dot colour, and start the pulse +
    /// animated ellipsis. Used for listening/transcribing/refining so the pill
    /// names exactly what's happening and looks alive.
    /// </summary>
    public void SetActive(string label, byte r, byte g, byte b)
    {
        StatusDot.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));

        _baseLabel = label;
        _ellipsisCount = 0;
        StatusText.Text = label + new string(' ', 3);   // reserve the ellipsis width
        _ellipsisTimer.Start();

        StatusDot.BeginAnimation(OpacityProperty, PulseAnimation);
    }

    /// <summary>
    /// Stop the live animations and restore a steady dot — for the static mode
    /// flash and when hiding, so nothing keeps ticking off-screen.
    /// </summary>
    public void StopAnimations()
    {
        _ellipsisTimer.Stop();
        StatusDot.BeginAnimation(OpacityProperty, null);
        StatusDot.Opacity = 1.0;
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

    /// <summary>
    /// Show an arbitrary mode label and dot colour, decided by Core's
    /// <see cref="FlexSlotOverlay"/>. Used for the Flex-slot tap-cycle feedback
    /// (issue: flex feedback) — a transient pill that names the mode just cycled
    /// to, instead of the throttled tray balloon.
    /// </summary>
    public void SetModeFlash(string label, byte r, byte g, byte b)
    {
        StopAnimations();   // a flash is a steady snapshot, not a live activity
        StatusText.Text = label;
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
