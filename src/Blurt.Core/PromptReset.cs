namespace Blurt.Core;

/// <summary>
/// The pure "reset every editable prompt to its shipped default" action behind the
/// Settings "Reset prompts to defaults" button (issue 37). It only replaces the prompt
/// fields with their <see cref="ModePrompts.DefaultFor"/> defaults — it deliberately
/// does <em>not</em> touch <see cref="BlurtConfig.PromptBackup"/>. The recoverable
/// backup is captured at save time by <see cref="PromptBackupPolicy"/>, so a reset is
/// recoverable the same way any other prompt change is: the previous prompts are backed
/// up when the reset is saved.
/// </summary>
public static class PromptReset
{
    /// <summary>
    /// Return <paramref name="config"/> with every editable prompt set to its shipped
    /// default. Idempotent: resetting an already-default config returns it unchanged.
    /// </summary>
    public static BlurtConfig Reset(BlurtConfig config) => config with
    {
        FixPrompt = ModePrompts.DefaultFor(RefinedMode.Fix),
        EnglishPrompt = ModePrompts.DefaultFor(RefinedMode.English),
        BulletsPrompt = ModePrompts.DefaultFor(RefinedMode.Bullets),
        EmailPrompt = ModePrompts.DefaultFor(RefinedMode.Email),
        CustomPrompt = ModePrompts.DefaultFor(RefinedMode.Custom),
    };
}
