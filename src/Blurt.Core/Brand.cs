namespace Blurt.Core;

/// <summary>
/// The Blurt brand colour — the single source of truth for the app mark's primary
/// colour (the speech-bubble body at rest). Used for the window/exe icon and the
/// idle tray icon; the recording/processing tray states tint the bubble with
/// <see cref="TrayPalette"/>'s red/amber instead. Kept GDI/WPF-free like the other
/// colour decisions so it is unit-testable and shared by every surface that paints
/// the mark.
/// </summary>
public static class Brand
{
    /// <summary>The primary brand blue (R,G,B) — the bubble body when idle.</summary>
    public static (byte R, byte G, byte B) Primary => (47, 111, 237);
}
