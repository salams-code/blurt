namespace Blurt.Core;

/// <summary>
/// The teaching cards shown right after first-run setup (issue 40), in lesson
/// order. Onboarding (issue 15) <em>configures</em> the app; this <em>teaches</em>
/// it — a newcomer otherwise has to discover push-to-talk, the three triggers and
/// the Flex tap/hold modes on their own. Pure ordering + copy, so the lesson's
/// sequence and wording are unit-testable and a reorder is a deliberate change;
/// the WPF <c>TutorialWindow</c> is the thin animated shell over this.
/// </summary>
public enum TutorialCard
{
    /// <summary>The core gesture: hold the key, speak, release — text at the cursor.</summary>
    PushToTalk,

    /// <summary>The three triggers (Fix / English / Flex) and what each does.</summary>
    Triggers,

    /// <summary>Flex: tap to cycle the mode, hold to dictate; the four modes.</summary>
    FlexModes,

    /// <summary>The overlay pill reads the live status (listening → transcribing → …).</summary>
    LiveStatus,

    /// <summary>A simulated full take so the gesture is seen once before going live.</summary>
    TryIt,
}

/// <summary>
/// The first-run teaching content (issue 40): the card order and each card's
/// title + body. The single source of truth for the lesson, so the App's window
/// only renders what Core decides — the same split as <see cref="Onboarding"/>
/// and the overlay wording (<see cref="StatusLabel"/>).
/// </summary>
public static class Tutorial
{
    /// <summary>
    /// The cards in teaching order: the core gesture first, then what the triggers
    /// do, then the Flex modes that build on a trigger, then the live-status pill,
    /// and finally a simulated take to watch.
    /// </summary>
    public static IReadOnlyList<TutorialCard> Cards { get; } =
    [
        TutorialCard.PushToTalk,
        TutorialCard.Triggers,
        TutorialCard.FlexModes,
        TutorialCard.LiveStatus,
        TutorialCard.TryIt,
    ];

    /// <summary>The short heading for <paramref name="card"/>.</summary>
    public static string Title(TutorialCard card) => card switch
    {
        TutorialCard.PushToTalk => "Push to talk",
        TutorialCard.Triggers => "Three ways to dictate",
        TutorialCard.FlexModes => "Flex: tap to switch, hold to talk",
        TutorialCard.LiveStatus => "The pill shows what's happening",
        TutorialCard.TryIt => "Watch a take",
        _ => card.ToString(),
    };

    /// <summary>The explanatory body for <paramref name="card"/>.</summary>
    public static string Body(TutorialCard card) => card switch
    {
        TutorialCard.PushToTalk =>
            "Hold the hotkey, speak, then let go. Your words land right where the "
            + "cursor is — no window to switch to, no button to click.",
        TutorialCard.Triggers =>
            "Each hotkey shapes the result: Fix tidies up what you said, English "
            + "translates as you go, and Flex is your switchable slot.",
        TutorialCard.FlexModes =>
            "Tap the Flex key to cycle the mode — Pur (verbatim, stays on your "
            + "machine), Bullets, Custom and Email. Hold it to dictate in the mode "
            + "you landed on.",
        TutorialCard.LiveStatus =>
            "A small pill follows along: listening while you talk, then transcribing "
            + "and fixing or bulleting — so you always know the step you're on.",
        TutorialCard.TryIt =>
            "Here's a full run from start to finish. When you're ready, close this "
            + "and try it for real — hold a hotkey and speak.",
        _ => "",
    };
}

/// <summary>
/// The animation cues the tutorial plays on the real overlay pill, sourced from
/// the very mappings the running app uses so what a newcomer learns is exactly
/// what they will see. <see cref="ModeFlashes"/> reuses the shipped Flex cycle
/// order and <see cref="StatusFrames"/> reuses the live <see cref="StatusLabel"/>
/// verbs — keeping the lesson honest if either ever changes.
/// </summary>
public static class TutorialDemo
{
    /// <summary>
    /// The modes the Flex card flashes, in the exact order the slot ships cycling
    /// through (<see cref="BlurtConfig.FlexSlotOrder"/>) — so the colours and names
    /// the lesson shows match a real tap-cycle.
    /// </summary>
    public static IReadOnlyList<FlexSlotMode> ModeFlashes => BlurtConfig.Default.FlexSlotOrder;

    /// <summary>
    /// The phases a simulated take steps through on the pill: listening while
    /// "speaking", then transcribing, then a refine verb — the same labels a real
    /// Fix dictation shows, so the demo is the genuine status pill, not a mock-up.
    /// </summary>
    public static IReadOnlyList<string> StatusFrames =>
    [
        StatusLabel.Listening,
        StatusLabel.Transcribing(local: false),
        StatusLabel.Fixing,
    ];
}
