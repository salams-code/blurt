namespace Blurt.Core;

/// <summary>Which Blurt dictation trigger a keystroke maps to.</summary>
public enum TriggerKind
{
    Fix,
    English,
    FlexSlot,
}

/// <summary>Whether a key event is a press (down) or a release (up).</summary>
public enum KeyEdge
{
    Down,
    Up,
}

/// <summary>A single raw key event as reported by the low-level keyboard hook.</summary>
public readonly record struct KeyInput(int VirtualKeyCode, KeyEdge Edge);

/// <summary>A recognised Blurt trigger event the app can act on.</summary>
public readonly record struct TriggerEvent(TriggerKind Kind, KeyEdge Edge);

/// <summary>
/// The hook adapter's instruction for one key event: whether to swallow the
/// keystroke (so it never reaches the focused app) and, if recognised, the
/// trigger event to raise.
/// </summary>
public readonly record struct KeyDecision(bool Swallow, TriggerEvent? Trigger);
