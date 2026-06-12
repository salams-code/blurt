namespace Blurt.Core;

/// <summary>
/// Press-to-capture for the settings hotkey fields (issue 20). The WPF field
/// reports the modifier state (is AltGr held?) and the pressed key's virtual-key
/// code; this pure decision says whether that is a valid Blurt trigger chord and,
/// if so, renders the chord string to show in the field. All of Blurt's triggers
/// are <c>AltGr + &lt;trigger character&gt;</c>, so a capture is valid only when
/// AltGr is held over one of the trigger keys; everything else is rejected so the
/// field never accepts a chord the hook can't fire. Builds on
/// <see cref="HotkeyBinding"/> so a captured chord parses back identically to a
/// hand-typed one.
/// </summary>
public static class HotkeyCapture
{
    /// <summary>
    /// Decides whether a key press in a hotkey field is a valid trigger chord.
    /// Succeeds (and yields the canonical chord text, e.g. <c>"AltGr+,"</c>) only
    /// when <paramref name="altGrHeld"/> is true and <paramref name="virtualKeyCode"/>
    /// is one of the trigger keys (<c>, . -</c>). Otherwise returns <c>false</c> with
    /// <paramref name="chord"/> set to the empty string, so the field leaves its
    /// current value untouched and the caller can show rejection guidance.
    /// </summary>
    public static bool TryCapture(bool altGrHeld, int virtualKeyCode, out string chord)
    {
        chord = "";

        // No AltGr, no chord: a bare trigger character is just typing, not a binding.
        if (!altGrHeld)
            return false;

        // Render the VK back through HotkeyBinding so only a known trigger key
        // produces a chord. Format falls back to a "VK_0x.." label for an unknown
        // code, which never round-trips through TryParse — use that to reject it.
        var formatted = HotkeyBinding.Format(virtualKeyCode);
        if (!HotkeyBinding.TryParse(formatted, out _))
            return false;

        chord = formatted;
        return true;
    }
}
