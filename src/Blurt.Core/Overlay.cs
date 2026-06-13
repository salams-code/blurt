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

/// <summary>
/// Pure mapping from a <see cref="FlexSlotMode"/> to how the overlay pill should
/// announce it on a tap-cycle. The tap's only feedback used to be a tray balloon,
/// which Windows throttles — so a quick second tap showed no change and the cycle
/// felt stuck. The overlay pill updates instantly and repeatedly instead, and each
/// mode gets a <em>distinct</em> label and dot colour so the mode you just cycled
/// to is unambiguous at a glance (not a single generic pill). Kept WPF-free so the
/// colour choice is unit-testable; the App paints a dot in that RGB.
/// </summary>
public static class FlexSlotOverlay
{
    /// <summary>The pill label for <paramref name="mode"/> — the mode's name, with
    /// a leading bullet for Bullets so the three read distinctly.</summary>
    public static string Label(FlexSlotMode mode) => mode switch
    {
        FlexSlotMode.Pur => "Pur",
        FlexSlotMode.Bullets => "• Bullets",
        FlexSlotMode.Custom => "Custom",
        FlexSlotMode.Email => "✉ Email",
        _ => mode.ToString(),
    };

    /// <summary>
    /// The status-dot colour for <paramref name="mode"/>, distinct per mode so the
    /// colour alone disambiguates: green = Pur (the offline/verbatim mode), blue =
    /// Bullets, purple = Custom, teal = Email. Deliberately none of the status
    /// colours (red/amber) and never the idle grey, so a mode flash never looks
    /// like a recording/processing pill.
    /// </summary>
    public static (byte R, byte G, byte B) Dot(FlexSlotMode mode) => mode switch
    {
        FlexSlotMode.Pur => (40, 167, 69),       // green
        FlexSlotMode.Bullets => (13, 110, 253),  // blue
        FlexSlotMode.Custom => (111, 66, 193),   // purple
        FlexSlotMode.Email => (23, 162, 184),    // teal
        _ => (128, 128, 128),                    // grey (unknown — should not happen)
    };
}

/// <summary>
/// The wording for the overlay's <em>live activity</em> — what the app is doing
/// right now, so the pill reads as a precise status rather than a generic
/// "busy". One source of truth for every phase's verb. Labels carry no trailing
/// "…": the overlay animates the ellipsis itself, so the base text stays put while
/// the dots cycle. Lowercase to match the pill's existing voice ("listening").
/// </summary>
public static class StatusLabel
{
    /// <summary>Recording in progress.</summary>
    public const string Listening = "listening";

    /// <summary>
    /// Transcribing the take. <paramref name="local"/> distinguishes on-device
    /// whisper.cpp ("transcribing locally") from the cloud API ("transcribing"),
    /// so the user can see whether their voice is leaving the machine — Pur is
    /// always local by contract, the cloud tiers say so explicitly.
    /// </summary>
    public static string Transcribing(bool local) => local ? "transcribing locally" : "transcribing";

    /// <summary>Refining via the Fix mode (German cleanup).</summary>
    public const string Fixing = "fixing";

    /// <summary>Refining via the Bullets mode (reformat to bullet points).</summary>
    public const string Bulleting = "bulleting";

    /// <summary>Refining via the Email mode (rewrite as a well-formed email).</summary>
    public const string Emailing = "emailing";

    /// <summary>Refining via the English mode (translate to English).</summary>
    public const string Translating = "translating";

    /// <summary>Refining via a user-defined Custom prompt.</summary>
    public const string Refining = "refining";
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
