namespace Blurt.Core;

/// <summary>
/// Stateful, headless core of the keyboard hook: turns the stream of single
/// raw key events the OS hook reports into Blurt trigger events, and decides
/// which keystrokes to swallow. The Win32 hook is a thin adapter over this.
/// </summary>
public sealed class TriggerResolver
{
    private const int VkRMenu = 0xA5;      // right Alt — the "AltGr" half

    /// <summary>
    /// The design-default AltGr bindings. The parameterless constructor uses
    /// these; the settings window builds a custom map from the persisted config
    /// (see <see cref="HotkeyBindings.ResolveVkMap"/>) when the user remaps.
    /// </summary>
    public static IReadOnlyDictionary<int, TriggerKind> DefaultBindings { get; } =
        new Dictionary<int, TriggerKind>
        {
            [0xBC] = TriggerKind.Fix,       // ','
            [0xBE] = TriggerKind.English,   // '.'
            [0xBD] = TriggerKind.FlexSlot,  // '-'
        };

    private static readonly KeyDecision PassThrough = new(Swallow: false, Trigger: null);
    private static readonly KeyDecision SwallowSilently = new(Swallow: true, Trigger: null);

    // Per-instance so a remap installs a fresh resolver with its own map (the hook
    // is re-created on save), and the defaults stay shared and immutable.
    private readonly IReadOnlyDictionary<int, TriggerKind> _bindings;

    private bool _rightAltDown;

    // The trigger key currently held down, so its release is swallowed too —
    // even if AltGr was let go first (otherwise the character leaks on key-up).
    private (int Vk, TriggerKind Kind)? _activeTrigger;

    /// <summary>Resolves triggers from the design-default AltGr bindings.</summary>
    public TriggerResolver() : this(DefaultBindings)
    {
    }

    /// <summary>
    /// Resolves triggers from a custom virtual-key → trigger map (e.g. one built
    /// from the user's remapped config). The map is used as-is — provide the full
    /// set of bindings, not a delta over the defaults.
    /// </summary>
    public TriggerResolver(IReadOnlyDictionary<int, TriggerKind> bindings)
    {
        _bindings = bindings;
    }

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
            && _bindings.TryGetValue(input.VirtualKeyCode, out var kind))
        {
            _activeTrigger = (input.VirtualKeyCode, kind);
            return new KeyDecision(Swallow: true, new TriggerEvent(kind, KeyEdge.Down));
        }

        return PassThrough;
    }
}
