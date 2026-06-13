using Blurt.Core;
using Xunit;

namespace Blurt.Core.Tests;

/// <summary>
/// The non-destructive prompt reset behind issue 37: a reset returns every editable
/// prompt to its shipped default, but first captures the current prompts into the
/// single <see cref="BlurtConfig.PromptBackup"/> slot so the pre-reset state is
/// always recoverable. There is no separate "Custom mode name" in Blurt, so the
/// editable surface — and what these tests cover — is the five mode prompts.
/// </summary>
public class PromptResetTests
{
    // A config with every editable prompt customised away from its default.
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
    public void Reset_backs_up_the_current_prompts_before_overwriting_them()
    {
        var original = Customised();

        var reset = PromptReset.Reset(original);

        // The backup is the snapshot of what the prompts were just before the reset.
        Assert.Equal(PromptSnapshot.From(original), reset.PromptBackup);
    }

    [Fact]
    public void The_backup_fully_recovers_the_pre_reset_prompts()
    {
        // Non-destructive (criterion 3): applying the backup restores every prompt
        // exactly — the seam the restore UI (issue 38) will use.
        var original = Customised();
        var reset = PromptReset.Reset(original);

        var restored = reset.PromptBackup!.ApplyTo(reset);

        Assert.Equal(original.FixPrompt, restored.FixPrompt);
        Assert.Equal(original.EnglishPrompt, restored.EnglishPrompt);
        Assert.Equal(original.BulletsPrompt, restored.BulletsPrompt);
        Assert.Equal(original.EmailPrompt, restored.EmailPrompt);
        Assert.Equal(original.CustomPrompt, restored.CustomPrompt);
    }

    [Fact]
    public void Resetting_an_all_default_config_is_a_safe_no_op()
    {
        // Nothing customised → the reset changes nothing and records no backup.
        Assert.Equal(BlurtConfig.Default, PromptReset.Reset(BlurtConfig.Default));
    }

    [Fact]
    public void A_redundant_reset_does_not_clobber_the_existing_backup()
    {
        // First reset backs up the user's customised prompts; the config is now all
        // default. A second reset must be a no-op so it can't replace that real
        // backup with a useless default snapshot — the recovery stays intact.
        var first = PromptReset.Reset(Customised());
        var second = PromptReset.Reset(first);

        Assert.Equal(first, second);
        Assert.Equal(first.PromptBackup, second.PromptBackup);
    }
}
