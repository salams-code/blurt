namespace Blurt.Core;

/// <summary>
/// The pure "reset every editable prompt to its shipped default" action (issue 37),
/// kept non-destructive: it first captures the current prompts into the single
/// <see cref="BlurtConfig.PromptBackup"/> slot, then applies the defaults, so a reset
/// is always recoverable (the restore UI is issue 38). Resetting a config that is
/// already all-default is a safe no-op — it changes nothing and, crucially, does not
/// overwrite an existing backup with a useless default snapshot.
/// </summary>
public static class PromptReset
{
    /// <summary>
    /// Reset <paramref name="config"/>'s editable prompts to their shipped defaults
    /// after backing up the current ones, or return it unchanged when nothing is
    /// customised (see the type summary).
    /// </summary>
    public static BlurtConfig Reset(BlurtConfig config)
    {
        // Nothing customised → nothing to reset, and nothing worth backing up. Return
        // unchanged so a redundant reset can't clobber a real backup with a default one.
        if (IsAllDefault(config))
            return config;

        return config with
        {
            PromptBackup = PromptSnapshot.From(config),
            FixPrompt = ModePrompts.DefaultFor(RefinedMode.Fix),
            EnglishPrompt = ModePrompts.DefaultFor(RefinedMode.English),
            BulletsPrompt = ModePrompts.DefaultFor(RefinedMode.Bullets),
            EmailPrompt = ModePrompts.DefaultFor(RefinedMode.Email),
            CustomPrompt = ModePrompts.DefaultFor(RefinedMode.Custom),
        };
    }

    /// <summary>
    /// True when every editable prompt already holds its shipped default verbatim —
    /// the "nothing customised" case a reset treats as a no-op. Compares the stored
    /// values (a blanked always-on prompt counts as customised: it differs from the
    /// default text even though it resolves to the default at dictation time).
    /// </summary>
    private static bool IsAllDefault(BlurtConfig config) =>
        config.FixPrompt == ModePrompts.DefaultFor(RefinedMode.Fix)
        && config.EnglishPrompt == ModePrompts.DefaultFor(RefinedMode.English)
        && config.BulletsPrompt == ModePrompts.DefaultFor(RefinedMode.Bullets)
        && config.EmailPrompt == ModePrompts.DefaultFor(RefinedMode.Email)
        && config.CustomPrompt == ModePrompts.DefaultFor(RefinedMode.Custom);
}
