using Blurt.Core;
using Xunit;

namespace Blurt.Core.Tests;

public class TrayPaletteTests
{
    [Fact]
    public void Recording_is_red()
    {
        // Red = the most "live" state: clearly dominant red channel.
        var (r, g, b) = TrayPalette.For(TrayState.Recording);

        Assert.True(r > g && r > b, $"expected a red-dominant colour, got ({r},{g},{b})");
    }

    [Fact]
    public void Processing_is_amber()
    {
        // Amber = strong red + green, little blue (a warm yellow-orange).
        var (r, g, b) = TrayPalette.For(TrayState.Processing);

        Assert.True(r > 150 && g > 120 && b < r && b < g,
            $"expected an amber colour, got ({r},{g},{b})");
    }

    [Fact]
    public void Idle_is_a_neutral_grey()
    {
        // Idle = neutral: the three channels are (near) equal, so no hue dominates.
        var (r, g, b) = TrayPalette.For(TrayState.Idle);

        Assert.Equal(r, g);
        Assert.Equal(g, b);
    }

    [Fact]
    public void Each_state_gets_a_distinct_colour()
    {
        var idle = TrayPalette.For(TrayState.Idle);
        var recording = TrayPalette.For(TrayState.Recording);
        var processing = TrayPalette.For(TrayState.Processing);

        Assert.NotEqual(idle, recording);
        Assert.NotEqual(idle, processing);
        Assert.NotEqual(recording, processing);
    }
}
