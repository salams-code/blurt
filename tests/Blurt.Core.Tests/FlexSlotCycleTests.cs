using System;
using System.Collections.Generic;
using Blurt.Core;
using Xunit;

namespace Blurt.Core.Tests;

public class FlexSlotCycleTests
{
    [Fact]
    public void Current_starts_at_the_first_mode_in_the_order()
    {
        // Default order is Pur → Bullets → Custom (BlurtConfig.Default.FlexSlotOrder),
        // so a fresh cycle reports Pur before anyone taps.
        var cycle = new FlexSlotCycle();

        Assert.Equal(FlexSlotMode.Pur, cycle.Current);
    }

    [Fact]
    public void Cycle_advances_through_the_default_order_and_wraps_around()
    {
        var cycle = new FlexSlotCycle();

        Assert.Equal(FlexSlotMode.Bullets, cycle.Cycle());
        Assert.Equal(FlexSlotMode.Bullets, cycle.Current);

        Assert.Equal(FlexSlotMode.Custom, cycle.Cycle());
        Assert.Equal(FlexSlotMode.Custom, cycle.Current);

        // Wrap-around: Custom → Pur.
        Assert.Equal(FlexSlotMode.Pur, cycle.Cycle());
        Assert.Equal(FlexSlotMode.Pur, cycle.Current);
    }

    [Fact]
    public void Cycle_respects_a_custom_order()
    {
        // A different order (and a different starting mode) proves the sequence
        // is injected, not hard-coded to the enum's declaration order.
        var order = new List<FlexSlotMode> { FlexSlotMode.Custom, FlexSlotMode.Pur };
        var cycle = new FlexSlotCycle(order);

        Assert.Equal(FlexSlotMode.Custom, cycle.Current);
        Assert.Equal(FlexSlotMode.Pur, cycle.Cycle());
        Assert.Equal(FlexSlotMode.Custom, cycle.Cycle());   // wraps back to the first
    }

    [Fact]
    public void A_single_element_order_always_resolves_to_that_mode()
    {
        var cycle = new FlexSlotCycle(new List<FlexSlotMode> { FlexSlotMode.Bullets });

        Assert.Equal(FlexSlotMode.Bullets, cycle.Current);
        Assert.Equal(FlexSlotMode.Bullets, cycle.Cycle());
        Assert.Equal(FlexSlotMode.Bullets, cycle.Current);
    }

    [Fact]
    public void An_empty_order_is_rejected()
    {
        // An empty order has no current mode to resolve — fail fast at construction
        // rather than throwing later on the first Cycle/Current access.
        Assert.Throws<ArgumentException>(() => new FlexSlotCycle(new List<FlexSlotMode>()));
    }
}
