using Blurt.Core;
using Xunit;

namespace Blurt.Core.Tests;

public class HotkeyBindingTests
{
    // The three design-default trigger characters and their Windows virtual-key
    // codes, as the low-level hook reports them.
    private const int VkOemComma = 0xBC;   // ','
    private const int VkOemPeriod = 0xBE;  // '.'
    private const int VkOemMinus = 0xBD;   // '-'

    [Theory]
    [InlineData("AltGr+,", VkOemComma)]
    [InlineData("AltGr+.", VkOemPeriod)]
    [InlineData("AltGr+-", VkOemMinus)]
    public void TryParse_recognises_the_default_altgr_chords(string text, int expectedVk)
    {
        Assert.True(HotkeyBinding.TryParse(text, out var vk));
        Assert.Equal(expectedVk, vk);
    }

    [Theory]
    [InlineData(",", VkOemComma)]
    [InlineData(".", VkOemPeriod)]
    [InlineData("-", VkOemMinus)]
    public void TryParse_accepts_a_bare_trigger_character_without_the_altgr_prefix(string text, int expectedVk)
    {
        Assert.True(HotkeyBinding.TryParse(text, out var vk));
        Assert.Equal(expectedVk, vk);
    }

    [Theory]
    [InlineData(VkOemComma, "AltGr+,")]
    [InlineData(VkOemPeriod, "AltGr+.")]
    [InlineData(VkOemMinus, "AltGr+-")]
    public void Format_renders_a_known_vk_as_its_altgr_chord(int vk, string expected)
    {
        Assert.Equal(expected, HotkeyBinding.Format(vk));
    }

    [Theory]
    [InlineData("AltGr+,")]
    [InlineData("AltGr+.")]
    [InlineData("AltGr+-")]
    public void Parse_then_format_round_trips_back_to_the_original_text(string text)
    {
        Assert.True(HotkeyBinding.TryParse(text, out var vk));
        Assert.Equal(text, HotkeyBinding.Format(vk));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("AltGr+")]
    [InlineData("AltGr+nonsense")]
    [InlineData("Ctrl+Shift+Q")]
    [InlineData("garbage")]
    public void TryParse_returns_false_and_zero_for_unrecognised_text(string text)
    {
        Assert.False(HotkeyBinding.TryParse(text, out var vk));
        Assert.Equal(0, vk);
    }

    [Fact]
    public void TryParse_returns_false_for_null()
    {
        Assert.False(HotkeyBinding.TryParse(null!, out var vk));
        Assert.Equal(0, vk);
    }

    [Fact]
    public void Format_of_an_unknown_vk_falls_back_to_a_hex_label_rather_than_throwing()
    {
        // 0x51 is 'Q' — not one of the trigger characters Blurt knows how to name.
        var label = HotkeyBinding.Format(0x51);
        Assert.False(string.IsNullOrWhiteSpace(label));
    }
}
