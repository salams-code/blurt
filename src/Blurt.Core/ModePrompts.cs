namespace Blurt.Core;

/// <summary>
/// The single editable source for each refined mode's system prompt (issue 35).
/// Maps a <see cref="RefinedMode"/> to the prompt the refiner should run, reading
/// the user's override from <see cref="BlurtConfig"/> and falling back to the
/// shipped default (the <see cref="RefinementPrompts"/> constants). Pure and
/// I/O-free so the "which prompt for which mode" rule is one reviewable, unit-tested
/// place; <see cref="FlexSlotPrompts"/> and the dictation pipeline both resolve
/// through it, so an edit in Settings takes effect on the next dictation with no
/// restart. Pur has no entry here — it is verbatim and promptless by design.
/// </summary>
public static class ModePrompts
{
    /// <summary>
    /// The shipped default prompt for <paramref name="mode"/>: the
    /// <see cref="RefinementPrompts"/> constant for the always-on modes (Fix,
    /// English, Bullets), and empty for Custom (which has no built-in wording —
    /// an empty prompt means "no refiner").
    /// </summary>
    public static string DefaultFor(RefinedMode mode) => mode switch
    {
        RefinedMode.Fix => RefinementPrompts.Fix,
        RefinedMode.English => RefinementPrompts.English,
        RefinedMode.Bullets => RefinementPrompts.Bullets,
        RefinedMode.Custom => "",
        _ => "",
    };

    /// <summary>
    /// The effective prompt for <paramref name="mode"/> given <paramref name="config"/>:
    /// the user's override when set, otherwise <see cref="DefaultFor"/>. Custom is
    /// returned verbatim — a blank Custom prompt stays blank so the caller can fall
    /// back to a verbatim dictation. For the always-on modes a blank override falls
    /// back to the default, so clearing the field can never silently disable the mode.
    /// </summary>
    public static string For(RefinedMode mode, BlurtConfig config)
    {
        var configured = StoredFor(mode, config);

        // Custom may legitimately be unset (blank ⇒ no refiner); never substitute a
        // default for it. The always-on modes always refine, so blank ⇒ default.
        if (mode == RefinedMode.Custom)
            return configured;

        return string.IsNullOrWhiteSpace(configured) ? DefaultFor(mode) : configured;
    }

    // The raw value persisted for the mode, before any blank → default handling.
    private static string StoredFor(RefinedMode mode, BlurtConfig config) => mode switch
    {
        RefinedMode.Fix => config.FixPrompt,
        RefinedMode.English => config.EnglishPrompt,
        RefinedMode.Bullets => config.BulletsPrompt,
        RefinedMode.Custom => config.CustomPrompt,
        _ => "",
    };
}
