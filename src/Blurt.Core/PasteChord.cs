namespace Blurt.Core;

/// <summary>
/// Builds the key-event sequence that pastes the clipboard into the focused
/// app. Headless companion to the SendInput adapter so the chord composition
/// is unit-testable.
/// </summary>
public static class PasteChord
{
    private const int VkControl = 0x11;
    private const int VkV = 0x56;

    /// <param name="heldModifierVks">
    /// Modifier keys physically held when the paste fires (the hook swallows
    /// the trigger key, but not the AltGr the user is still holding).
    /// </param>
    public static IReadOnlyList<KeyInput> Build(IReadOnlyCollection<int> heldModifierVks) =>
    [
        // Release whatever the user still holds (typically AltGr after the
        // trigger key came up), otherwise the target app sees Ctrl+Alt+V.
        .. heldModifierVks.Select(vk => new KeyInput(vk, KeyEdge.Up)),
        new KeyInput(VkControl, KeyEdge.Down),
        new KeyInput(VkV, KeyEdge.Down),
        new KeyInput(VkV, KeyEdge.Up),
        new KeyInput(VkControl, KeyEdge.Up),
    ];
}
