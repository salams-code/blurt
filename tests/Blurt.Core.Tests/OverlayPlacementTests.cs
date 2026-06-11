using Blurt.Core;
using Xunit;

namespace Blurt.Core.Tests;

public class OverlayPlacementTests
{
    // A generous 1920×1080 screen at the origin, and a typical pill size, used by
    // most cases so the interesting numbers stand out.
    private static readonly OverlayBounds Screen = new(0, 0, 1920, 1080);
    private static readonly OverlaySize Pill = new(160, 40);

    [Fact]
    public void Mouse_anchor_places_the_pill_offset_from_the_pointer()
    {
        // Comfortably inside the screen so no clamping interferes — the result is
        // purely the mouse position plus the fixed offset.
        var mouse = new OverlayPoint(500, 400);

        var p = OverlayPlacement.Resolve(OverlayAnchor.MousePointer, mouse, Pill, Screen);

        Assert.Equal(516, p.X);   // 500 + 16
        Assert.Equal(416, p.Y);   // 400 + 16
    }

    [Fact]
    public void Mouse_anchor_clamps_at_the_right_edge()
    {
        // Pointer hard against the right edge: the offset would push the pill off
        // screen, so X pins to the rightmost spot that still fits the whole pill.
        var mouse = new OverlayPoint(1910, 400);

        var p = OverlayPlacement.Resolve(OverlayAnchor.MousePointer, mouse, Pill, Screen);

        Assert.Equal(1920 - 160, p.X);   // 1760
    }

    [Fact]
    public void Mouse_anchor_clamps_at_the_bottom_edge()
    {
        var mouse = new OverlayPoint(500, 1075);

        var p = OverlayPlacement.Resolve(OverlayAnchor.MousePointer, mouse, Pill, Screen);

        Assert.Equal(1080 - 40, p.Y);   // 1040
    }

    [Fact]
    public void Mouse_anchor_clamps_at_the_left_edge()
    {
        // A screen that starts at a negative origin (e.g. a monitor left of the
        // primary): a pointer past the left edge — far enough that even after the
        // +16 offset the pill would start off-screen — pins to that edge.
        var screen = new OverlayBounds(-1920, 0, 1920, 1080);
        var mouse = new OverlayPoint(-1950, 400);

        var p = OverlayPlacement.Resolve(OverlayAnchor.MousePointer, mouse, Pill, screen);

        Assert.Equal(-1920, p.X);   // pinned to the screen's left edge
    }

    [Fact]
    public void Mouse_anchor_clamps_at_the_top_edge()
    {
        // Far enough above the top that even after the +16 offset the pill's top
        // would still be negative, so it pins to the screen's top edge.
        var mouse = new OverlayPoint(500, -30);

        var p = OverlayPlacement.Resolve(OverlayAnchor.MousePointer, mouse, Pill, Screen);

        Assert.Equal(0, p.Y);   // pinned to the screen's top edge
    }

    [Fact]
    public void BottomCenter_centres_horizontally_and_sits_above_the_bottom()
    {
        // Independent of the mouse: centred in X, a fixed margin above the bottom.
        var mouse = new OverlayPoint(123, 456);

        var p = OverlayPlacement.Resolve(OverlayAnchor.BottomCenter, mouse, Pill, Screen);

        Assert.Equal((1920 - 160) / 2, p.X);          // 880, centred
        Assert.Equal(1080 - 40 - 48, p.Y);            // above the bottom edge by the margin
    }

    [Fact]
    public void BottomCenter_respects_a_non_origin_screen()
    {
        // A secondary monitor offset to the right and down: centring is relative
        // to that screen's own bounds, not the desktop origin.
        var screen = new OverlayBounds(1920, 100, 1280, 720);
        var mouse = new OverlayPoint(0, 0);

        var p = OverlayPlacement.Resolve(OverlayAnchor.BottomCenter, mouse, Pill, screen);

        Assert.Equal(1920 + (1280 - 160) / 2, p.X);          // centred within the screen
        Assert.Equal(100 + 720 - 40 - 48, p.Y);              // margin above its bottom edge
    }
}
