namespace Blurt.Core;

/// <summary>
/// Stateful, headless core of the keyboard hook: turns the stream of single
/// raw key events the OS hook reports into Blurt trigger events, and decides
/// which keystrokes to swallow. The Win32 hook is a thin adapter over this.
/// </summary>
public sealed class TriggerResolver
{
    private const int VkRMenu = 0xA5;      // right Alt — the "AltGr" half

    // Default AltGr bindings (remappable later, issue 07+).
    private static readonly IReadOnlyDictionary<int, TriggerKind> Bindings = new Dictionary<int, TriggerKind>
    {
        [0xBC] = TriggerKind.Fix,       // ','
        [0xBE] = TriggerKind.English,   // '.'
        [0xBD] = TriggerKind.FlexSlot,  // '-'
    };

    private static readonly KeyDecision PassThrough = new(Swallow: false, Trigger: null);
    private static readonly KeyDecision SwallowSilently = new(Swallow: true, Trigger: null);

    private bool _rightAltDown;

    // The trigger key currently held down, so its release is swallowed too —
    // even if AltGr was let go first (otherwise the character leaks on key-up).
    private (int Vk, TriggerKind Kind)? _activeTrigger;

    public KeyDecision Process(KeyInput input)
    {
        if (input.VirtualKeyCode == VkRMenu)
        {
            _rightAltDown = input.Edge == KeyEdge.Down;
            return PassThrough;
        }

        if (input.Edge == KeyEdge.Up
            && _activeTrigger is { } active
            && active.Vk == input.VirtualKeyCode)
        {
            _activeTrigger = null;
            return new KeyDecision(Swallow: true, new TriggerEvent(active.Kind, KeyEdge.Up));
        }

        // OS auto-repeat while the trigger key is held: keep swallowing, but
        // a trigger fires exactly one Down (on press) and one Up (on release).
        if (input.Edge == KeyEdge.Down
            && _activeTrigger is { } held
            && held.Vk == input.VirtualKeyCode)
        {
            return SwallowSilently;
        }

        if (input.Edge == KeyEdge.Down
            && _rightAltDown
            && Bindings.TryGetValue(input.VirtualKeyCode, out var kind))
        {
            _activeTrigger = (input.VirtualKeyCode, kind);
            return new KeyDecision(Swallow: true, new TriggerEvent(kind, KeyEdge.Down));
        }

        return PassThrough;
    }
}
