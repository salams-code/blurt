using Blurt.Core;
using Xunit;

namespace Blurt.Core.Tests;

/// <summary>
/// Press-to-capture decision for the settings hotkey fields (issue 20): given the
/// modifier state and the pressed key's virtual-key code that the WPF field
/// observes, decide whether it is a valid Blurt trigger chord and, if so, render
/// the chord string the field should show. The WPF PreviewKeyDown handler is the
/// thin shell over this; all the decision logic is here and unit-tested.
/// </summary>
public class HotkeyCaptureTests
{
    private const int VkOemComma = 0xBC;   // ','
    private const int VkOemPeriod = 0xBE;  // '.'
    private const int VkOemMinus = 0xBD;   // '-'
    private const int VkQ = 0x51;          // a non-trigger key

    [Theory]
    [InlineData(VkOemComma, "AltGr+,")]
    [InlineData(VkOemPeriod, "AltGr+.")]
    [InlineData(VkOemMinus, "AltGr+-")]
    public void Capture_with_altgr_on_a_trigger_key_yields_its_chord(int vk, string expected)
    {
        Assert.True(HotkeyCapture.TryCapture(altGrHeld: true, vk, out var chord));
        Assert.Equal(expected, chord);
    }

    [Theory]
    [InlineData(VkOemComma)]
    [InlineData(VkOemPeriod)]
    [InlineData(VkOemMinus)]
    public void Capture_without_altgr_is_rejected_even_on_a_trigger_key(int vk)
    {
        // Blurt's triggers are all AltGr chords; a bare trigger character isn't a
        // valid capture (it's just typing the character), so reject it.
        Assert.False(HotkeyCapture.TryCapture(altGrHeld: false, vk, out var chord));
        Assert.Equal("", chord);
    }

    [Theory]
    [InlineData(VkQ)]
    [InlineData(0x41)]   // 'A'
    [InlineData(0x20)]   // space
    public void Capture_with_altgr_on_a_non_trigger_key_is_rejected(int vk)
    {
        // AltGr held but the key isn't one of , . - — not a Blurt chord.
        Assert.False(HotkeyCapture.TryCapture(altGrHeld: true, vk, out var chord));
        Assert.Equal("", chord);
    }

    [Fact]
    public void Captured_chord_round_trips_through_HotkeyBinding()
    {
        // The chord string a capture produces must parse back to the same VK, so a
        // captured hotkey is indistinguishable from a hand-typed one downstream.
        Assert.True(HotkeyCapture.TryCapture(altGrHeld: true, VkOemComma, out var chord));
        Assert.True(HotkeyBinding.TryParse(chord, out var vk));
        Assert.Equal(VkOemComma, vk);
    }
}
