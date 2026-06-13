using Blurt.Core;
using Xunit;

namespace Blurt.Core.Tests;

public class FlexSlotOverlayTests
{
    [Theory]
    [InlineData(FlexSlotMode.Pur, "Pur")]
    [InlineData(FlexSlotMode.Bullets, "• Bullets")]
    [InlineData(FlexSlotMode.Custom, "Custom")]
    [InlineData(FlexSlotMode.Email, "✉ Email")]
    public void Each_mode_has_its_own_label(FlexSlotMode mode, string expected)
    {
        Assert.Equal(expected, FlexSlotOverlay.Label(mode));
    }

    [Fact]
    public void Every_mode_gets_a_distinct_dot_colour_so_the_mode_is_unambiguous()
    {
        var colours = new[]
        {
            FlexSlotOverlay.Dot(FlexSlotMode.Pur),
            FlexSlotOverlay.Dot(FlexSlotMode.Bullets),
            FlexSlotOverlay.Dot(FlexSlotMode.Custom),
            FlexSlotOverlay.Dot(FlexSlotMode.Email),
        };

        Assert.Equal(4, colours.Distinct().Count());
    }

    [Theory]
    [InlineData(FlexSlotMode.Pur)]
    [InlineData(FlexSlotMode.Bullets)]
    [InlineData(FlexSlotMode.Custom)]
    [InlineData(FlexSlotMode.Email)]
    public void A_mode_dot_is_never_the_status_or_idle_colours(FlexSlotMode mode)
    {
        // A mode flash must not look like a recording (red), processing (amber) or
        // idle (grey) pill — those carry a different meaning.
        var dot = FlexSlotOverlay.Dot(mode);

        Assert.NotEqual(TrayPalette.For(TrayState.Recording), dot);
        Assert.NotEqual(TrayPalette.For(TrayState.Processing), dot);
        Assert.NotEqual(TrayPalette.For(TrayState.Idle), dot);
    }
}
