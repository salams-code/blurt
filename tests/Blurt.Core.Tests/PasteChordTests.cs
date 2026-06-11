using Blurt.Core;
using Xunit;

namespace Blurt.Core.Tests;

public class PasteChordTests
{
    private const int VkControl = 0x11;
    private const int VkV = 0x56;
    private const int VkRMenu = 0xA5;   // right Alt — still physically held in the natural AltGr gesture

    [Fact]
    public void With_no_modifiers_held_the_chord_is_a_plain_ctrl_v()
    {
        var chord = PasteChord.Build(heldModifierVks: []);

        Assert.Equal(
        [
            new KeyInput(VkControl, KeyEdge.Down),
            new KeyInput(VkV, KeyEdge.Down),
            new KeyInput(VkV, KeyEdge.Up),
            new KeyInput(VkControl, KeyEdge.Up),
        ], chord);
    }

    [Fact]
    public void Held_modifiers_are_released_before_the_chord_so_ctrl_v_is_not_corrupted()
    {
        // Natural gesture: the trigger key is released first, AltGr is still
        // down when the paste fires — without the release the target app
        // would see Ctrl+Alt+V instead of Ctrl+V.
        var chord = PasteChord.Build(heldModifierVks: [VkRMenu]);

        Assert.Equal(
        [
            new KeyInput(VkRMenu, KeyEdge.Up),
            new KeyInput(VkControl, KeyEdge.Down),
            new KeyInput(VkV, KeyEdge.Down),
            new KeyInput(VkV, KeyEdge.Up),
            new KeyInput(VkControl, KeyEdge.Up),
        ], chord);
    }
}
