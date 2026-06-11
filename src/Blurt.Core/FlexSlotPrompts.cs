namespace Blurt.Core;

/// <summary>
/// Maps a <see cref="FlexSlotMode"/> to the system prompt the refiner should run,
/// the pure decision behind a Flex-slot hold. Kept out of the Win32 glue so the
/// "which mode sends which prompt" rule is unit-testable on its own:
/// <list type="bullet">
///   <item>Pur → no prompt (the caller must skip the refiner — zero network).</item>
///   <item>Bullets → the shared <see cref="RefinementPrompts.Bullets"/> constant.</item>
///   <item>Custom → the user's <see cref="BlurtConfig.CustomPrompt"/>.</item>
/// </list>
/// A blank prompt (Pur, or a Custom mode the user never configured) is reported
/// as <c>null</c>, the agreed "no refiner" signal: the caller dictates the raw
/// transcript instead of sending an empty system prompt to the model.
/// </summary>
public static class FlexSlotPrompts
{
    /// <summary>
    /// The system prompt for <paramref name="mode"/> given <paramref name="config"/>,
    /// or <c>null</c> when the mode should bypass the refiner (Pur, or a Custom
    /// mode with no prompt set).
    /// </summary>
    public static string? For(FlexSlotMode mode, BlurtConfig config)
    {
        var prompt = mode switch
        {
            FlexSlotMode.Bullets => RefinementPrompts.Bullets,
            FlexSlotMode.Custom => config.CustomPrompt,
            // Pur (and any future mode) carries no prompt: skip the refiner.
            _ => null,
        };

        // Normalise an unset/blank prompt to null so callers have one "no refiner"
        // sentinel to test, whether the blank came from Pur or an empty Custom.
        return string.IsNullOrWhiteSpace(prompt) ? null : prompt;
    }
}
