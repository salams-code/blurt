using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Blurt.Core;

namespace Blurt.App;

/// <summary>
/// Builds the idle / recording / processing tray icons programmatically — the Blurt
/// mark (<see cref="BlurtLogo"/>: a speech bubble + waveform) with its bubble body
/// tinted by status, so the tray icon doubles as the app's logo and its live status
/// light: brand blue when idle (<see cref="Brand"/>), red/amber while recording/
/// processing (<see cref="TrayPalette"/>). No image asset is shipped. The three
/// icons are created once and cached; <see cref="Dispose"/> frees the underlying GDI
/// handles (created via <c>GetHicon</c>, which must be released with
/// <c>DestroyIcon</c> to avoid leaking handles).
/// </summary>
internal sealed class TrayIcons : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);

    private readonly Dictionary<TrayState, Icon> _icons = new();
    private readonly List<IntPtr> _handles = new();

    public TrayIcons()
    {
        foreach (var state in new[] { TrayState.Idle, TrayState.Recording, TrayState.Processing })
        {
            _icons[state] = CreateIcon(state);
        }
    }

    /// <summary>The cached icon for <paramref name="state"/>.</summary>
    public Icon For(TrayState state) => _icons[state];

    // Draw the 16×16 Blurt mark with its bubble tinted for the state and turn it into
    // an Icon. The bitmap's HICON handle is tracked so it can be destroyed on dispose
    // — Icon.FromHandle does not own/free the handle.
    private Icon CreateIcon(TrayState state)
    {
        using var bitmap = new Bitmap(16, 16, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var (r, gr, b) = BubbleColour(state);
            BlurtLogo.Draw(g, 0, 0, 16, Color.FromArgb(r, gr, b), Color.White);
        }

        var handle = bitmap.GetHicon();
        _handles.Add(handle);
        return Icon.FromHandle(handle);
    }

    // The bubble body's colour for a state: the brand mark when idle, the status
    // red/amber (TrayPalette) while recording/processing — the same colour language
    // as the overlay pill, now wrapped in the logo.
    private static (byte R, byte G, byte B) BubbleColour(TrayState state) => state switch
    {
        TrayState.Recording => TrayPalette.For(TrayState.Recording),
        TrayState.Processing => TrayPalette.For(TrayState.Processing),
        _ => Brand.Primary,
    };

    public void Dispose()
    {
        foreach (var icon in _icons.Values)
        {
            icon.Dispose();
        }
        _icons.Clear();

        foreach (var handle in _handles)
        {
            DestroyIcon(handle);
        }
        _handles.Clear();
    }
}
