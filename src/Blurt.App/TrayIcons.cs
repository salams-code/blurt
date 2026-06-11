using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Blurt.Core;

namespace Blurt.App;

/// <summary>
/// Builds the idle / recording / processing tray icons programmatically from
/// Core's <see cref="TrayPalette"/> — a filled coloured dot — so no image assets
/// are shipped and the colours stay the single decision in Core. The three icons
/// are created once and cached; <see cref="Dispose"/> frees the underlying GDI
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
            _icons[state] = CreateIcon(TrayPalette.For(state));
        }
    }

    /// <summary>The cached icon for <paramref name="state"/>.</summary>
    public Icon For(TrayState state) => _icons[state];

    // Draw a 16×16 filled circle in the state colour and turn it into an Icon. The
    // bitmap's HICON handle is tracked so it can be destroyed on dispose — Icon
    // from FromHandle does not own/free the handle.
    private Icon CreateIcon((byte R, byte G, byte B) rgb)
    {
        using var bitmap = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(Color.FromArgb(rgb.R, rgb.G, rgb.B));
            g.FillEllipse(brush, 1, 1, 14, 14);
        }

        var handle = bitmap.GetHicon();
        _handles.Add(handle);
        return Icon.FromHandle(handle);
    }

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
