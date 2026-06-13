using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Blurt.Core;

namespace Blurt.App;

/// <summary>
/// Draws the Blurt app mark — a rounded speech bubble (a circle with a bottom-left
/// tail) holding three white waveform bars — in GDI, so one piece of vector-ish
/// geometry serves every surface: the status-tinted tray icon
/// (<see cref="TrayIcons"/>) and the multi-resolution window/exe <c>.ico</c>. No
/// raster asset is shipped; the shape plus the brand colour (<see cref="Brand"/>)
/// are the single source. The bubble colour is a parameter so the tray can tint the
/// body by status while the icon keeps the brand blue; the bars are passed too
/// (white on every tint).
/// </summary>
internal static class BlurtLogo
{
    /// <summary>
    /// Fill the mark into <paramref name="g"/> within a <paramref name="size"/>-square
    /// box at (<paramref name="x"/>, <paramref name="y"/>). Call with AntiAlias on.
    /// </summary>
    public static void Draw(Graphics g, float x, float y, float size, Color bubble, Color bars)
    {
        var dia = size * 0.72f;
        var left = x + (size - dia) / 2f;
        var top = y + size * 0.05f;
        var cx = left + dia / 2f;
        var cy = top + dia / 2f;

        using var bubbleBrush = new SolidBrush(bubble);

        // Bubble body (a circle) + a tail beak at the bottom-left, drawn in the same
        // colour and overlapping so anti-aliasing fuses them into one speech bubble.
        g.FillEllipse(bubbleBrush, left, top, dia, dia);
        using (var tail = new GraphicsPath())
        {
            tail.AddPolygon(new[]
            {
                new PointF(cx - dia * 0.30f, cy + dia * 0.26f),
                new PointF(cx - dia * 0.40f, cy + dia * 0.60f),
                new PointF(cx + dia * 0.00f, cy + dia * 0.40f),
            });
            g.FillPath(bubbleBrush, tail);
        }

        // Three rounded waveform bars, the middle tallest, centred in the bubble.
        var barWidth = size * 0.105f;
        var gap = size * 0.075f;
        var heights = new[] { 0.42f, 0.62f, 0.42f };
        var total = 3 * barWidth + 2 * gap;
        var startX = cx - total / 2f;

        using var barBrush = new SolidBrush(bars);
        for (var i = 0; i < 3; i++)
        {
            var h = dia * heights[i];
            using var bar = RoundedRect(startX + i * (barWidth + gap), cy - h / 2f, barWidth, h, barWidth / 2f);
            g.FillPath(barBrush, bar);
        }
    }

    /// <summary>Render the mark to a transparent <paramref name="size"/>² PNG.</summary>
    public static byte[] RenderPng(int size, Color bubble, Color bars)
    {
        using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            Draw(g, 0, 0, size, bubble, bars);
        }

        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    /// <summary>
    /// Write a multi-resolution <c>.ico</c> (PNG-encoded entries) of the brand mark
    /// to <paramref name="path"/> — used to (re)generate the committed window/exe
    /// icon via the <c>--export-icon</c> dev flag.
    /// </summary>
    public static void ExportBrandIco(string path)
    {
        var bubble = Color.FromArgb(Brand.Primary.R, Brand.Primary.G, Brand.Primary.B);
        var sizes = new[] { 16, 24, 32, 48, 64, 128, 256 };
        var entries = sizes.Select(s => (size: s, png: RenderPng(s, bubble, Color.White))).ToList();

        var full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);

        using var fs = File.Create(full);
        using var w = new BinaryWriter(fs);
        w.Write((short)0);               // reserved
        w.Write((short)1);               // type: icon
        w.Write((short)entries.Count);

        var offset = 6 + entries.Count * 16;
        foreach (var (size, png) in entries)
        {
            w.Write((byte)(size >= 256 ? 0 : size));   // width  (0 ⇒ 256)
            w.Write((byte)(size >= 256 ? 0 : size));   // height (0 ⇒ 256)
            w.Write((byte)0);                          // palette count
            w.Write((byte)0);                          // reserved
            w.Write((short)1);                         // colour planes
            w.Write((short)32);                        // bits per pixel
            w.Write(png.Length);
            w.Write(offset);
            offset += png.Length;
        }

        foreach (var (_, png) in entries)
            w.Write(png);
    }

    private static GraphicsPath RoundedRect(float x, float y, float w, float h, float r)
    {
        var d = 2 * r;
        var p = new GraphicsPath();
        p.AddArc(x, y, d, d, 180, 90);
        p.AddArc(x + w - d, y, d, d, 270, 90);
        p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        p.AddArc(x, y + h - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}
