using Blurt.Core;
using Xunit;

namespace Blurt.Core.Tests;

public class BrandTests
{
    [Fact]
    public void Primary_is_the_brand_blue()
    {
        Assert.Equal(((byte)47, (byte)111, (byte)237), Brand.Primary);
    }

    [Fact]
    public void Idle_brand_is_distinct_from_the_active_tray_states()
    {
        // The idle tray mark uses the brand colour; it must never be confusable with
        // the recording (red) or processing (amber) status tints.
        Assert.NotEqual(TrayPalette.For(TrayState.Recording), Brand.Primary);
        Assert.NotEqual(TrayPalette.For(TrayState.Processing), Brand.Primary);
    }
}
