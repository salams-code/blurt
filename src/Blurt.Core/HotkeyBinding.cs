namespace Blurt.Core;

/// <summary>
/// Pure translation between a hotkey's human-readable chord text (as stored in
/// <see cref="BlurtConfig.HotkeyBindings"/>, e.g. <c>"AltGr+,"</c>) and the
/// Windows virtual-key code the low-level keyboard hook reports. Blurt's three
/// triggers are all <c>AltGr + &lt;character&gt;</c>, so the parser only needs to
/// recognise the trigger character; the <c>AltGr</c> half is handled by the hook
/// (right-Alt) and the resolver. Unknown text yields <c>false</c> so the settings
/// UI can reject garbage and fall back to defaults rather than crash.
/// </summary>
public static class HotkeyBinding
{
    private const string AltGrPrefix = "AltGr+";

    // The trigger characters Blurt understands, paired with their OEM virtual-key
    // codes. Kept tiny on purpose: these are the only chords the design contract
    // assigns, and the format stays human-readable in config.json.
    private static readonly IReadOnlyList<(string Character, int Vk)> KnownChords =
    [
        (",", 0xBC),   // VK_OEM_COMMA
        (".", 0xBE),   // VK_OEM_PERIOD
        ("-", 0xBD),   // VK_OEM_MINUS
    ];

    /// <summary>
    /// Parses a chord description into its virtual-key code. Accepts the canonical
    /// <c>"AltGr+,"</c> form and the bare character (<c>","</c>); the trailing
    /// character is what identifies the key. Returns <c>false</c> (and <c>vk = 0</c>)
    /// for null, empty, or anything whose character isn't a known trigger.
    /// </summary>
    public static bool TryParse(string text, out int virtualKeyCode)
    {
        virtualKeyCode = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var trimmed = text.Trim();

        // Strip the optional AltGr prefix; what remains must be exactly the
        // trigger character. (A lone "AltGr+" leaves an empty remainder → no match.)
        if (trimmed.StartsWith(AltGrPrefix, StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[AltGrPrefix.Length..];

        foreach (var (character, vk) in KnownChords)
        {
            if (trimmed == character)
            {
                virtualKeyCode = vk;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Renders a virtual-key code back as its canonical chord text for display.
    /// A known trigger key becomes e.g. <c>"AltGr+,"</c>; an unknown code falls
    /// back to a hex label (<c>"VK_0x51"</c>) so the UI never shows a blank.
    /// </summary>
    public static string Format(int virtualKeyCode)
    {
        foreach (var (character, vk) in KnownChords)
        {
            if (vk == virtualKeyCode)
                return AltGrPrefix + character;
        }

        return $"VK_0x{virtualKeyCode:X2}";
    }
}
