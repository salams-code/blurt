namespace Blurt.Core;

/// <summary>
/// The outcome of validating a <see cref="BlurtConfig"/>: valid when no problems
/// were found, otherwise a list of human-readable messages the settings window
/// can show inline. Value-based so it's easy to assert on in tests.
/// </summary>
public sealed record SettingsValidationResult(IReadOnlyList<string> Errors)
{
    /// <summary>True when there are no validation errors.</summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>A passing result with no errors.</summary>
    public static SettingsValidationResult Valid { get; } = new([]);
}

/// <summary>
/// Pure validation of a <see cref="BlurtConfig"/> before it is persisted. Catches
/// the two ways the settings window can produce an unusable config: two triggers
/// remapped onto the same key (one would shadow the other in the hook), and a
/// refinement base URL that isn't an absolute http/https endpoint (the refiner
/// can't call it). All problems are collected so the UI can show them together.
/// </summary>
public static class SettingsValidation
{
    /// <summary>
    /// Validates <paramref name="config"/>, returning every problem found. An empty
    /// error list (<see cref="SettingsValidationResult.IsValid"/>) means it is safe
    /// to save.
    /// </summary>
    public static SettingsValidationResult Validate(BlurtConfig config)
    {
        var errors = new List<string>();

        ValidateHotkeys(config, errors);
        ValidateBaseUrl(config, errors);

        return new SettingsValidationResult(errors);
    }

    // A hotkey conflict = two triggers resolving to the same virtual-key code. We
    // parse each chord and group by VK; any VK claimed by more than one trigger is
    // reported. Unparseable chords are left to ResolveVkMap's fallback, so they
    // don't show up as conflicts here.
    private static void ValidateHotkeys(BlurtConfig config, List<string> errors)
    {
        var byVk = new Dictionary<int, List<TriggerKind>>();

        foreach (var (trigger, chord) in config.HotkeyBindings)
        {
            if (HotkeyBinding.TryParse(chord, out var vk))
            {
                if (!byVk.TryGetValue(vk, out var triggers))
                    byVk[vk] = triggers = [];
                triggers.Add(trigger);
            }
        }

        foreach (var (vk, triggers) in byVk)
        {
            if (triggers.Count > 1)
            {
                var names = string.Join(", ", triggers);
                errors.Add(
                    $"Hotkey conflict: {names} are all bound to {HotkeyBinding.Format(vk)}. " +
                    "Each trigger needs its own key.");
            }
        }
    }

    // A usable refinement endpoint is an absolute http/https URL. Uri.TryCreate
    // with Absolute rejects relative paths and missing schemes; the scheme check
    // rejects ftp/file/etc.
    private static void ValidateBaseUrl(BlurtConfig config, List<string> errors)
    {
        if (!Uri.TryCreate(config.RefinementBaseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            errors.Add(
                $"Refinement base URL is not a valid http(s) address: " +
                $"\"{config.RefinementBaseUrl}\".");
        }
    }
}
