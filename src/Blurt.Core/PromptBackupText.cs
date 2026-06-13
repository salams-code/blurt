namespace Blurt.Core;

/// <summary>
/// Renders a <see cref="PromptSnapshot"/> as human-readable text for the prompt-backup
/// UI (issue 38): the single labelled block the Settings window both shows on screen
/// and copies to the clipboard, so "view" and "copy" share one wording. Pure and
/// I/O-free — each backed-up prompt is shown under its mode name so the reader can
/// tell them apart (Blurt has no separate editable mode name; the names are the modes).
/// </summary>
public static class PromptBackupText
{
    /// <summary>The labelled, multi-line view of <paramref name="backup"/>.</summary>
    public static string Format(PromptSnapshot backup) => string.Join(
        "\n\n",
        $"Fix:\n{backup.FixPrompt}",
        $"English:\n{backup.EnglishPrompt}",
        $"Bullets:\n{backup.BulletsPrompt}",
        $"Email:\n{backup.EmailPrompt}",
        $"Custom:\n{backup.CustomPrompt}");
}
