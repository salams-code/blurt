namespace Blurt.Core;

/// <summary>
/// Decides the single prompt-backup slot at save time (issue 38 follow-up). The
/// backup is "the prompts as they were at the previous save": whenever a save changes
/// any editable prompt, the previous prompts are captured (overwriting the old backup)
/// so "Restore backup" undoes that change — whether it came from an edit or from a
/// reset to defaults. A save that leaves every prompt untouched keeps the existing
/// backup, so an unrelated settings change can't wipe a useful one. Pure and I/O-free.
/// </summary>
public static class PromptBackupPolicy
{
    /// <summary>
    /// The backup to persist when saving <paramref name="next"/> over the
    /// previously-saved <paramref name="previous"/>: a snapshot of
    /// <paramref name="previous"/>'s prompts when they differ from
    /// <paramref name="next"/>'s, otherwise <paramref name="previous"/>'s existing backup.
    /// </summary>
    public static PromptSnapshot? OnSave(BlurtConfig previous, BlurtConfig next)
    {
        var previousPrompts = PromptSnapshot.From(previous);

        // Prompts unchanged → don't touch the backup (a non-prompt save must not
        // overwrite a still-useful backup with an identical snapshot).
        if (previousPrompts == PromptSnapshot.From(next))
            return previous.PromptBackup;

        // A prompt changed → the previous prompts become the recoverable backup.
        return previousPrompts;
    }
}
