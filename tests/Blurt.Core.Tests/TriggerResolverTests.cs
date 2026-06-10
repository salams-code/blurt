using Blurt.Core;
using Xunit;

namespace Blurt.Core.Tests;

public class TriggerResolverTests
{
    // Windows virtual-key codes the low-level hook reports.
    private const int VkRMenu = 0xA5;      // right Alt (the "AltGr" half)
    private const int VkOemComma = 0xBC;   // ','
    private const int VkOemPeriod = 0xBE;  // '.'
    private const int VkOemMinus = 0xBD;   // '-'

    [Fact]
    public void AltGr_plus_comma_down_is_a_swallowed_Fix_trigger()
    {
        var resolver = new TriggerResolver();

        resolver.Process(new KeyInput(VkRMenu, KeyEdge.Down));
        var decision = resolver.Process(new KeyInput(VkOemComma, KeyEdge.Down));

        Assert.True(decision.Swallow);
        Assert.Equal(new TriggerEvent(TriggerKind.Fix, KeyEdge.Down), decision.Trigger);
    }

    [Fact]
    public void Comma_without_right_alt_passes_through_and_is_not_a_trigger()
    {
        var resolver = new TriggerResolver();

        var decision = resolver.Process(new KeyInput(VkOemComma, KeyEdge.Down));

        Assert.False(decision.Swallow);
        Assert.Null(decision.Trigger);
    }

    [Fact]
    public void Trigger_key_up_is_a_swallowed_Up_event_even_after_right_alt_released()
    {
        var resolver = new TriggerResolver();

        resolver.Process(new KeyInput(VkRMenu, KeyEdge.Down));
        resolver.Process(new KeyInput(VkOemComma, KeyEdge.Down));
        resolver.Process(new KeyInput(VkRMenu, KeyEdge.Up));   // user lets go of AltGr first

        var decision = resolver.Process(new KeyInput(VkOemComma, KeyEdge.Up));

        Assert.True(decision.Swallow);
        Assert.Equal(new TriggerEvent(TriggerKind.Fix, KeyEdge.Up), decision.Trigger);
    }

    [Theory]
    [InlineData(VkOemPeriod, TriggerKind.English)]
    [InlineData(VkOemMinus, TriggerKind.FlexSlot)]
    public void AltGr_plus_period_or_minus_maps_to_its_trigger(int triggerVk, TriggerKind expected)
    {
        var resolver = new TriggerResolver();

        resolver.Process(new KeyInput(VkRMenu, KeyEdge.Down));
        var decision = resolver.Process(new KeyInput(triggerVk, KeyEdge.Down));

        Assert.True(decision.Swallow);
        Assert.Equal(new TriggerEvent(expected, KeyEdge.Down), decision.Trigger);
    }

    [Fact]
    public void Auto_repeat_down_of_held_trigger_key_is_swallowed_without_a_new_event()
    {
        var resolver = new TriggerResolver();

        resolver.Process(new KeyInput(VkRMenu, KeyEdge.Down));
        resolver.Process(new KeyInput(VkOemComma, KeyEdge.Down));

        var repeat = resolver.Process(new KeyInput(VkOemComma, KeyEdge.Down)); // OS auto-repeat while held

        Assert.True(repeat.Swallow);
        Assert.Null(repeat.Trigger);
    }

    [Fact]
    public void Auto_repeat_down_is_still_swallowed_after_right_alt_was_released_first()
    {
        var resolver = new TriggerResolver();

        resolver.Process(new KeyInput(VkRMenu, KeyEdge.Down));
        resolver.Process(new KeyInput(VkOemComma, KeyEdge.Down));
        resolver.Process(new KeyInput(VkRMenu, KeyEdge.Up));   // user lets go of AltGr first

        var repeat = resolver.Process(new KeyInput(VkOemComma, KeyEdge.Down)); // ',' must not leak

        Assert.True(repeat.Swallow);
        Assert.Null(repeat.Trigger);
    }

    [Fact]
    public void Right_alt_itself_passes_through()
    {
        var resolver = new TriggerResolver();

        var down = resolver.Process(new KeyInput(VkRMenu, KeyEdge.Down));
        var up = resolver.Process(new KeyInput(VkRMenu, KeyEdge.Up));

        Assert.False(down.Swallow);
        Assert.Null(down.Trigger);
        Assert.False(up.Swallow);
        Assert.Null(up.Trigger);
    }

    [Fact]
    public void Non_trigger_key_passes_through_even_while_altgr_is_held()
    {
        const int vkQ = 0x51; // 'Q' — with AltGr this is the '@' character, must not be swallowed
        var resolver = new TriggerResolver();

        resolver.Process(new KeyInput(VkRMenu, KeyEdge.Down));
        var decision = resolver.Process(new KeyInput(vkQ, KeyEdge.Down));

        Assert.False(decision.Swallow);
        Assert.Null(decision.Trigger);
    }
}
