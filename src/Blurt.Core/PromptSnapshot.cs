namespace Blurt.Core;

/// <summary>
/// A snapshot of every editable mode prompt (issue 37): the single backup slot a
/// "reset to defaults" captures before it overwrites the live prompts, so a reset
/// is always recoverable. Init-only properties (not positional) so it round-trips
/// through <see cref="SettingsStore"/>'s JSON the same way <see cref="RefinementEndpoint"/>
/// does, and a config written before this slot existed deserialises to <c>null</c>
/// (no backup). Value-based record equality compares the prompts structurally.
/// There is no separate "Custom mode name" in Blurt — the editable surface is these
/// five prompts, so the snapshot covers all of it.
/// </summary>
public sealed record PromptSnapshot
{
    /// <summary>The Fix mode's prompt at capture time.</summary>
    public string FixPrompt { get; init; } = "";

    /// <summary>The English mode's prompt at capture time.</summary>
    public string EnglishPrompt { get; init; } = "";

    /// <summary>The Bullets mode's prompt at capture time.</summary>
    public string BulletsPrompt { get; init; } = "";

    /// <summary>The Email mode's prompt at capture time.</summary>
    public string EmailPrompt { get; init; } = "";

    /// <summary>The Custom mode's prompt at capture time.</summary>
    public string CustomPrompt { get; init; } = "";

    /// <summary>Capture the editable prompts currently held in <paramref name="config"/>.</summary>
    public static PromptSnapshot From(BlurtConfig config) => new()
    {
        FixPrompt = config.FixPrompt,
        EnglishPrompt = config.EnglishPrompt,
        BulletsPrompt = config.BulletsPrompt,
        EmailPrompt = config.EmailPrompt,
        CustomPrompt = config.CustomPrompt,
    };

    /// <summary>
    /// Return <paramref name="config"/> with this snapshot's prompts applied — the
    /// pure "restore" used to recover a backup (the UI half is issue 38). Touches
    /// only the prompt fields; everything else on the config is left as-is.
    /// </summary>
    public BlurtConfig ApplyTo(BlurtConfig config) => config with
    {
        FixPrompt = FixPrompt,
        EnglishPrompt = EnglishPrompt,
        BulletsPrompt = BulletsPrompt,
        EmailPrompt = EmailPrompt,
        CustomPrompt = CustomPrompt,
    };
}
