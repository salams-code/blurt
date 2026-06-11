namespace Blurt.Core;

/// <summary>
/// The visible state of the status overlay pill (design contract §9). Drives both
/// the pill's text/colour and — paired with <see cref="TrayState"/> — the tray
/// icon. Kept WPF-free so the App layer's <c>OverlayController</c> can map it to a
/// window without Core depending on any UI framework.
/// </summary>
public enum OverlayState
{
    /// <summary>The pill is not shown.</summary>
    Hidden,

    /// <summary>Recording is in progress ("listening…").</summary>
    Listening,

    /// <summary>The take is being transcribed/refined ("transcribing…").</summary>
    Transcribing,
}

/// <summary>
/// Pure mapping from an <see cref="OverlayState"/> to the text the pill shows.
/// The single place the wording lives, so the App's WPF window only renders what
/// Core decides. <see cref="OverlayState.Hidden"/> yields the empty string — the
/// pill is invisible, so it has nothing to say.
/// </summary>
public static class OverlayText
{
    /// <summary>The label for <paramref name="state"/>; empty when hidden.</summary>
    public static string For(OverlayState state) => state switch
    {
        OverlayState.Listening => "listening…",
        OverlayState.Transcribing => "transcribing…",
        _ => "",
    };
}

/// <summary>A point in screen coordinates, kept WPF-free for pure placement logic.</summary>
public readonly record struct OverlayPoint(double X, double Y);

/// <summary>The size of the overlay pill, in the same units as <see cref="OverlayPoint"/>.</summary>
public readonly record struct OverlaySize(double Width, double Height);

/// <summary>A rectangle (e.g. the working area of the screen the pill must stay within).</summary>
public readonly record struct OverlayBounds(double X, double Y, double Width, double Height);

/// <summary>
/// Pure placement of the overlay pill. Computes the pill's top-left corner from
/// the chosen <see cref="OverlayAnchor"/>, the current mouse position, the pill's
/// size and the screen it lives on — clamping so the pill never spills off-screen.
/// The App adapter feeds it real cursor/screen coordinates and applies the result
/// to the WPF window; keeping the maths here makes every edge case unit-testable.
/// </summary>
public static class OverlayPlacement
{
    /// <summary>Gap (px) between the mouse pointer and the pill in MousePointer mode.</summary>
    private const double MouseOffset = 16;

    /// <summary>Gap (px) above the bottom edge in BottomCenter mode.</summary>
    private const double BottomMargin = 48;

    /// <summary>
    /// The pill's top-left corner for <paramref name="anchor"/>, given the
    /// <paramref name="mouse"/> position, the <paramref name="overlay"/> size and
    /// the <paramref name="screen"/> bounds. Always clamped so the whole pill
    /// stays inside <paramref name="screen"/>.
    /// </summary>
    public static OverlayPoint Resolve(
        OverlayAnchor anchor, OverlayPoint mouse, OverlaySize overlay, OverlayBounds screen)
    {
        var (x, y) = anchor switch
        {
            // Bottom-centre: horizontally centred in the screen, a fixed margin
            // above its bottom edge — independent of the mouse.
            OverlayAnchor.BottomCenter => (
                screen.X + (screen.Width - overlay.Width) / 2,
                screen.Y + screen.Height - overlay.Height - BottomMargin),

            // Mouse pointer: down-and-right of the cursor by a small offset so the
            // pill sits near the pointer without covering it.
            _ => (mouse.X + MouseOffset, mouse.Y + MouseOffset),
        };

        return new OverlayPoint(
            Clamp(x, screen.X, screen.X + screen.Width - overlay.Width),
            Clamp(y, screen.Y, screen.Y + screen.Height - overlay.Height));
    }

    // Clamp to [min, max]. When the pill is wider/taller than the screen the
    // range inverts (max < min); pin to min (the top-left edge) so it stays
    // anchored to a visible corner rather than off-screen.
    private static double Clamp(double value, double min, double max)
    {
        if (max < min) return min;
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
}

/// <summary>
/// The tray icon's status, mirroring <see cref="OverlayState"/> for the tray
/// channel. Separate enum because the tray has its own neutral "idle" resting
/// state (the overlay is simply hidden then), and to keep the colour mapping a
/// pure, testable decision independent of how the icon is drawn.
/// </summary>
public enum TrayState
{
    /// <summary>Resting — not recording or processing.</summary>
    Idle,

    /// <summary>Recording in progress.</summary>
    Recording,

    /// <summary>Transcribing/refining the take.</summary>
    Processing,
}

/// <summary>
/// Pure mapping from a <see cref="TrayState"/> to the RGB colour the tray icon is
/// painted in. Only the colour <em>choice</em> lives here (so it is unit-testable
/// in net8.0 with no GDI types); the App layer draws a filled dot in that colour.
/// </summary>
public static class TrayPalette
{
    /// <summary>
    /// The (R,G,B) for <paramref name="state"/>: neutral grey when idle, red while
    /// recording, amber while processing — the same colour language as the
    /// overlay's status dot.
    /// </summary>
    public static (byte R, byte G, byte B) For(TrayState state) => state switch
    {
        TrayState.Recording => (220, 53, 69),    // red
        TrayState.Processing => (255, 170, 0),   // amber
        _ => (128, 128, 128),                    // neutral grey (idle)
    };
}
