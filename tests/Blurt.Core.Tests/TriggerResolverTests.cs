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

    [Fact]
    public void Custom_bindings_resolve_the_remapped_vk_to_its_trigger()
    {
        const int vkF1 = 0x70; // a key the defaults never bind
        var resolver = new TriggerResolver(new Dictionary<int, TriggerKind>
        {
            [vkF1] = TriggerKind.Fix,
        });

        resolver.Process(new KeyInput(VkRMenu, KeyEdge.Down));
        var decision = resolver.Process(new KeyInput(vkF1, KeyEdge.Down));

        Assert.True(decision.Swallow);
        Assert.Equal(new TriggerEvent(TriggerKind.Fix, KeyEdge.Down), decision.Trigger);
    }

    [Fact]
    public void Custom_bindings_ignore_a_vk_the_defaults_used_to_map()
    {
        // Remap Fix onto F1 only: the old default ',' must no longer trigger.
        var resolver = new TriggerResolver(new Dictionary<int, TriggerKind>
        {
            [0x70] = TriggerKind.Fix,
        });

        resolver.Process(new KeyInput(VkRMenu, KeyEdge.Down));
        var decision = resolver.Process(new KeyInput(VkOemComma, KeyEdge.Down));

        Assert.False(decision.Swallow);
        Assert.Null(decision.Trigger);
    }

    // Shift virtual-key codes the low-level hook reports.
    private const int VkLShift = 0xA0;
    private const int VkRShift = 0xA1;

    [Fact]
    public void Shift_held_with_the_chord_tags_the_trigger_also_translate_and_still_swallows()
    {
        // Issue 39: AltGr+Shift+, is still a swallowed Fix trigger, but tagged
        // also-translate so the dictation layers an English translation on top.
        var resolver = new TriggerResolver();

        resolver.Process(new KeyInput(VkLShift, KeyEdge.Down));
        resolver.Process(new KeyInput(VkRMenu, KeyEdge.Down));
        var decision = resolver.Process(new KeyInput(VkOemComma, KeyEdge.Down));

        Assert.True(decision.Swallow);
        Assert.Equal(new TriggerEvent(TriggerKind.Fix, KeyEdge.Down, AlsoTranslate: true), decision.Trigger);
    }

    [Fact]
    public void Without_shift_the_trigger_is_not_tagged_also_translate()
    {
        // The default behaviour is unchanged: no Shift → no English layer.
        var resolver = new TriggerResolver();

        resolver.Process(new KeyInput(VkRMenu, KeyEdge.Down));
        var decision = resolver.Process(new KeyInput(VkOemComma, KeyEdge.Down));

        Assert.Equal(new TriggerEvent(TriggerKind.Fix, KeyEdge.Down, AlsoTranslate: false), decision.Trigger);
    }

    [Fact]
    public void Releasing_shift_before_the_chord_clears_the_also_translate_tag()
    {
        // The modifier is read at the moment the chord is pressed: Shift let go first
        // means a plain (untranslated) dictation.
        var resolver = new TriggerResolver();

        resolver.Process(new KeyInput(VkRShift, KeyEdge.Down));
        resolver.Process(new KeyInput(VkRShift, KeyEdge.Up));
        resolver.Process(new KeyInput(VkRMenu, KeyEdge.Down));
        var decision = resolver.Process(new KeyInput(VkOemComma, KeyEdge.Down));

        Assert.Equal(new TriggerEvent(TriggerKind.Fix, KeyEdge.Down, AlsoTranslate: false), decision.Trigger);
    }

    [Fact]
    public void A_shift_key_passes_through_untouched()
    {
        // Shift is a normal modifier, not a Blurt trigger: observe it, but never
        // swallow it or raise a trigger of its own.
        var resolver = new TriggerResolver();

        var decision = resolver.Process(new KeyInput(VkLShift, KeyEdge.Down));

        Assert.False(decision.Swallow);
        Assert.Null(decision.Trigger);
    }
}
