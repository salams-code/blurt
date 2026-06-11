namespace Blurt.Core;

/// <summary>
/// The system prompts that drive each refined dictation mode. Kept as a small,
/// pure source of strings (no I/O, no dependencies) so the exact wording is one
/// reviewable place and is unit-testable. A later mode registry (issue 11) will
/// pair these with their triggers; for now only the Fix prompt exists.
/// </summary>
public static class RefinementPrompts
{
    /// <summary>
    /// The Fix-mode system prompt: clean up a German voice transcript without
    /// changing its meaning. Whisper output of spontaneous speech carries filler
    /// words ("ähm", "also"), missing punctuation and small grammar slips; the
    /// refiner repairs those and returns polished German only — never a
    /// translation, an answer, or commentary.
    /// </summary>
    public const string Fix =
        "You are a German writing assistant. Clean up the user's dictated German " +
        "text: fix grammar, spelling and punctuation, and remove filler words " +
        "(e.g. \"ähm\", \"also\", \"halt\") and false starts. Preserve the original " +
        "meaning, tone and wording as much as possible — do not translate, answer, " +
        "summarise or add anything. Always respond in German with only the cleaned-up " +
        "text and no extra explanation.";
}
