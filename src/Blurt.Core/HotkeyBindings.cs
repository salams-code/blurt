namespace Blurt.Core;

/// <summary>
/// Builds the virtual-key → trigger map the <see cref="TriggerResolver"/> needs
/// from the persisted <see cref="BlurtConfig"/>. Pure and total: every trigger
/// ends up in the map, so a missing or unparseable chord can never make a trigger
/// silently unreachable — it falls back to that trigger's design default.
/// </summary>
public static class HotkeyBindings
{
    /// <summary>
    /// Resolves <paramref name="config"/>'s chord strings into the VK→trigger map.
    /// Each <see cref="TriggerKind"/> is taken from the config when its chord parses
    /// (<see cref="HotkeyBinding.TryParse"/>); otherwise it falls back to the
    /// design-default chord for that trigger. The result always contains all three
    /// triggers.
    /// </summary>
    public static IReadOnlyDictionary<int, TriggerKind> ResolveVkMap(BlurtConfig config)
    {
        var map = new Dictionary<int, TriggerKind>();

        foreach (var trigger in Enum.GetValues<TriggerKind>())
        {
            if (config.HotkeyBindings.TryGetValue(trigger, out var chord)
                && HotkeyBinding.TryParse(chord, out var vk))
            {
                map[vk] = trigger;
            }
            else if (HotkeyBinding.TryParse(DefaultChordFor(trigger), out var defaultVk))
            {
                map[defaultVk] = trigger;
            }
        }

        return map;
    }

    // The built-in chord a trigger reverts to when its configured chord is missing
    // or garbage. Sourced from the same defaults BlurtConfig ships with, so the two
    // never drift apart.
    private static string DefaultChordFor(TriggerKind trigger) =>
        BlurtConfig.Default.HotkeyBindings.TryGetValue(trigger, out var chord)
            ? chord
            : string.Empty;
}
