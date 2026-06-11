namespace Blurt.Core;

/// <summary>
/// Holds the Flex slot's current refinement mode and advances it on each tap.
/// Pure state with no Win32 dependency: <see cref="Cycle"/> steps one position
/// along the injected order (default <c>BlurtConfig.Default.FlexSlotOrder</c>,
/// i.e. Pur → Bullets → Custom) and wraps around, so the slot endlessly rotates
/// through its modes as the user taps the key. <see cref="Current"/> exposes the
/// mode in effect, which the tray reflects after each cycle.
/// </summary>
public sealed class FlexSlotCycle
{
    private readonly IReadOnlyList<FlexSlotMode> _order;
    private int _index;

    /// <param name="order">
    /// The rotation order. Defaults to <c>BlurtConfig.Default.FlexSlotOrder</c>
    /// so the slot follows the configured sequence. Must be non-empty.
    /// </param>
    public FlexSlotCycle(IReadOnlyList<FlexSlotMode>? order = null)
    {
        _order = order ?? BlurtConfig.Default.FlexSlotOrder;
        if (_order.Count == 0)
        {
            throw new ArgumentException("Flex-slot order must contain at least one mode.", nameof(order));
        }
    }

    /// <summary>The mode currently selected — what a hold would dictate with.</summary>
    public FlexSlotMode Current => _order[_index];

    /// <summary>
    /// Advances to the next mode in the order (wrapping past the end back to the
    /// start) and returns the new <see cref="Current"/>.
    /// </summary>
    public FlexSlotMode Cycle()
    {
        _index = (_index + 1) % _order.Count;
        return Current;
    }
}
