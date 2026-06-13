using Blurt.Core;
using Xunit;

namespace Blurt.Core.Tests;

/// <summary>
/// "Reset prompts to defaults" (issue 37): a pure field replacement that returns every
/// editable prompt to its shipped default. The recoverable backup is no longer this
/// type's job — it is captured at save time by <see cref="PromptBackupPolicy"/> — so a
/// reset must not, by itself, write or clear <see cref="BlurtConfig.PromptBackup"/>.
/// </summary>
public class PromptResetTests
{
    private static BlurtConfig Customised() => BlurtConfig.Default with
    {
        FixPrompt = "my fix",
        EnglishPrompt = "my english",
        BulletsPrompt = "my bullets",
        EmailPrompt = "my email",
        CustomPrompt = "my custom",
    };

    [Fact]
    public void Reset_returns_every_editable_prompt_to_its_shipped_default()
    {
        var reset = PromptReset.Reset(Customised());

        Assert.Equal(ModePrompts.DefaultFor(RefinedMode.Fix), reset.FixPrompt);
        Assert.Equal(ModePrompts.DefaultFor(RefinedMode.English), reset.EnglishPrompt);
        Assert.Equal(ModePrompts.DefaultFor(RefinedMode.Bullets), reset.BulletsPrompt);
        Assert.Equal(ModePrompts.DefaultFor(RefinedMode.Email), reset.EmailPrompt);
        Assert.Equal(ModePrompts.DefaultFor(RefinedMode.Custom), reset.CustomPrompt);
    }

    [Fact]
    public void Reset_does_not_touch_the_backup_slot()
    {
        // The backup is a save-time concern now (PromptBackupPolicy); resetting the
        // prompts must neither create a backup nor clear an existing one.
        var existing = new PromptSnapshot { FixPrompt = "kept" };
        var config = Customised() with { PromptBackup = existing };

        var reset = PromptReset.Reset(config);

        Assert.Equal(existing, reset.PromptBackup);
    }

    [Fact]
    public void Resetting_an_already_default_config_changes_nothing()
    {
        Assert.Equal(BlurtConfig.Default, PromptReset.Reset(BlurtConfig.Default));
    }
}
