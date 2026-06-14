using Blurt.Core;
using Xunit;

namespace Blurt.Core.Tests;

public class DriverNudgeTests
{
    [Theory]
    // driverMissing, vulkanLoaded, alreadyDismissed, expected
    [InlineData(true, false, false, true)]    // the one firing case: driver missing, no Vulkan, not yet dismissed
    [InlineData(true, false, true, false)]    // already dismissed → never again
    [InlineData(true, true, false, false)]    // the GPU actually works (Vulkan loaded) → nothing to nudge about
    [InlineData(false, false, false, false)]  // named GPU without Vulkan → status line only, NOT a nudge
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, true, true, false)]
    [InlineData(true, true, true, false)]
    public void ShouldShow_fires_only_when_driver_missing_and_no_vulkan_and_not_dismissed(
        bool driverMissing, bool vulkanLoaded, bool alreadyDismissed, bool expected)
    {
        Assert.Equal(
            expected,
            DriverNudge.ShouldShow(driverMissing, vulkanLoaded, alreadyDismissed));
    }

    [Fact]
    public void A_named_gpu_without_vulkan_does_not_nudge_only_the_status_line_covers_it()
    {
        // ADR-0001: the nudge is deliberately conservative — high confidence, low
        // false positives. A real AMD/NVIDIA/Intel GPU that simply lacks Vulkan
        // (driverMissingSignal=false, the adapter is named, not the basic fallback)
        // must NOT fire the nudge; the Settings status line (issue 44) already reports
        // the CPU fallback.
        Assert.False(DriverNudge.ShouldShow(
            driverMissingSignal: false, vulkanLoaded: false, alreadyDismissed: false));
    }
}
