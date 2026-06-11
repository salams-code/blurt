using Blurt.Core;
using Xunit;

namespace Blurt.Core.Tests;

public class HotkeyBindingsTests
{
    private const int VkOemComma = 0xBC;
    private const int VkOemPeriod = 0xBE;
    private const int VkOemMinus = 0xBD;

    [Fact]
    public void ResolveVkMap_maps_the_default_config_to_the_default_vk_bindings()
    {
        var map = HotkeyBindings.ResolveVkMap(BlurtConfig.Default);

        Assert.Equal(TriggerKind.Fix, map[VkOemComma]);
        Assert.Equal(TriggerKind.English, map[VkOemPeriod]);
        Assert.Equal(TriggerKind.FlexSlot, map[VkOemMinus]);
        Assert.Equal(3, map.Count);
    }

    [Fact]
    public void ResolveVkMap_parses_each_configured_chord_into_its_vk()
    {
        var config = BlurtConfig.Default with
        {
            HotkeyBindings = new Dictionary<TriggerKind, string>
            {
                [TriggerKind.Fix] = ".",       // remapped onto period
                [TriggerKind.English] = "-",   // remapped onto minus
                [TriggerKind.FlexSlot] = ",",  // remapped onto comma
            },
        };

        var map = HotkeyBindings.ResolveVkMap(config);

        Assert.Equal(TriggerKind.Fix, map[VkOemPeriod]);
        Assert.Equal(TriggerKind.English, map[VkOemMinus]);
        Assert.Equal(TriggerKind.FlexSlot, map[VkOemComma]);
    }

    [Fact]
    public void ResolveVkMap_falls_back_to_the_default_chord_for_an_unparseable_entry()
    {
        // English carries garbage; the other two are valid. The bad entry must not
        // throw and must not vanish — it falls back to English's default chord.
        var config = BlurtConfig.Default with
        {
            HotkeyBindings = new Dictionary<TriggerKind, string>
            {
                [TriggerKind.Fix] = "AltGr+,",
                [TriggerKind.English] = "totally-bogus",
                [TriggerKind.FlexSlot] = "AltGr+-",
            },
        };

        var map = HotkeyBindings.ResolveVkMap(config);

        Assert.Equal(TriggerKind.Fix, map[VkOemComma]);
        Assert.Equal(TriggerKind.FlexSlot, map[VkOemMinus]);
        // English fell back to its built-in default ('.') rather than disappearing.
        Assert.Equal(TriggerKind.English, map[VkOemPeriod]);
    }

    [Fact]
    public void ResolveVkMap_fills_in_a_default_for_a_trigger_missing_from_the_config()
    {
        // Config only binds Fix; English and FlexSlot must still resolve so no
        // trigger silently becomes unreachable.
        var config = BlurtConfig.Default with
        {
            HotkeyBindings = new Dictionary<TriggerKind, string>
            {
                [TriggerKind.Fix] = "AltGr+,",
            },
        };

        var map = HotkeyBindings.ResolveVkMap(config);

        Assert.Equal(TriggerKind.Fix, map[VkOemComma]);
        Assert.Equal(TriggerKind.English, map[VkOemPeriod]);
        Assert.Equal(TriggerKind.FlexSlot, map[VkOemMinus]);
    }
}
