using Blurt.Core;
using Xunit;

namespace Blurt.Core.Tests;

/// <summary>
/// The pure read/restore logic behind the prompt-backup UI (issue 38): the backup is
/// rendered to one labelled block for both the on-screen view and the clipboard, and
/// restored back into the live prompts via <see cref="PromptSnapshot.ApplyTo"/>. There
/// is no separate editable "mode name" in Blurt, so the names shown are the mode names.
/// </summary>
public class PromptBackupTests
{
    private static PromptSnapshot Backup() => new()
    {
        FixPrompt = "FIX-PROMPT",
        EnglishPrompt = "ENGLISH-PROMPT",
        BulletsPrompt = "BULLETS-PROMPT",
        EmailPrompt = "EMAIL-PROMPT",
        CustomPrompt = "CUSTOM-PROMPT",
    };

    [Fact]
    public void Format_labels_every_backed_up_prompt_with_its_mode_name()
    {
        var text = PromptBackupText.Format(Backup());

        // Each mode name is present as a label...
        foreach (var name in new[] { "Fix", "English", "Bullets", "Email", "Custom" })
            Assert.Contains(name, text);

        // ...next to its backed-up prompt value.
        foreach (var value in new[]
                 { "FIX-PROMPT", "ENGLISH-PROMPT", "BULLETS-PROMPT", "EMAIL-PROMPT", "CUSTOM-PROMPT" })
            Assert.Contains(value, text);
    }

    [Fact]
    public void Restoring_a_backup_puts_the_backed_up_prompts_back_as_the_active_prompts()
    {
        // Restore (criterion 4) is the inverse of reset: the backed-up prompts become
        // the live ones. Because dictation reads prompts per-take via ModePrompts.For,
        // a saved restore takes effect on the next dictation with no restart.
        var backup = Backup();
        var live = BlurtConfig.Default with
        {
            FixPrompt = "live fix",
            EnglishPrompt = "live english",
            BulletsPrompt = "live bullets",
            EmailPrompt = "live email",
            CustomPrompt = "live custom",
            PromptBackup = backup,
        };

        var restored = backup.ApplyTo(live);

        Assert.Equal("FIX-PROMPT", ModePrompts.For(RefinedMode.Fix, restored));
        Assert.Equal("ENGLISH-PROMPT", ModePrompts.For(RefinedMode.English, restored));
        Assert.Equal("BULLETS-PROMPT", ModePrompts.For(RefinedMode.Bullets, restored));
        Assert.Equal("EMAIL-PROMPT", ModePrompts.For(RefinedMode.Email, restored));
        Assert.Equal("CUSTOM-PROMPT", ModePrompts.For(RefinedMode.Custom, restored));
    }
}
