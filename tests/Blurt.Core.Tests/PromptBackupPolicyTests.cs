using Blurt.Core;
using Xunit;

namespace Blurt.Core.Tests;

/// <summary>
/// The save-time backup policy (issue 38 follow-up): on every save, if the prompts
/// being saved differ from the previously-saved ones, the previous prompts become the
/// single recoverable backup; a save that left the prompts untouched keeps whatever
/// backup was already there. This makes "Restore backup" undo the last prompt change —
/// whether that change came from an edit or from "Reset prompts to defaults".
/// </summary>
public class PromptBackupPolicyTests
{
    [Fact]
    public void Saving_changed_prompts_backs_up_the_previously_saved_prompts()
    {
        var previous = BlurtConfig.Default with
        {
            FixPrompt = "old fix",
            EnglishPrompt = "old english",
        };
        var next = previous with { FixPrompt = "new fix" };   // a prompt changed

        var backup = PromptBackupPolicy.OnSave(previous, next);

        // The backup is the prompts exactly as they were before this save.
        Assert.Equal(PromptSnapshot.From(previous), backup);
    }

    [Fact]
    public void Saving_without_changing_any_prompt_keeps_the_existing_backup()
    {
        // A save that only touches a non-prompt setting must not wipe a useful backup.
        var existing = new PromptSnapshot { FixPrompt = "earlier fix" };
        var previous = BlurtConfig.Default with { CustomPrompt = "same", PromptBackup = existing };
        var next = previous with { SoundEnabled = !previous.SoundEnabled };

        var backup = PromptBackupPolicy.OnSave(previous, next);

        Assert.Equal(existing, backup);
    }

    [Fact]
    public void The_backed_up_prompts_fully_restore_the_previous_state()
    {
        // The captured backup, applied back, recovers the pre-save prompts verbatim.
        var previous = BlurtConfig.Default with
        {
            FixPrompt = "F", EnglishPrompt = "E", BulletsPrompt = "B",
            EmailPrompt = "M", CustomPrompt = "C",
        };
        var next = previous with { BulletsPrompt = "changed" };

        var backup = PromptBackupPolicy.OnSave(previous, next);
        var restored = backup!.ApplyTo(next);

        Assert.Equal(PromptSnapshot.From(previous), PromptSnapshot.From(restored));
    }
}
