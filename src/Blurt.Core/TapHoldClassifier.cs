namespace Blurt.Core;

/// <summary>
/// The two ways the Flex-slot key can be used, decided purely by how long it was
/// held: a quick <see cref="Tap"/> cycles the slot mode, a sustained
/// <see cref="Hold"/> dictates with the current mode.
/// </summary>
public enum TapOrHold
{
    /// <summary>Key released before the threshold — cycle the slot mode.</summary>
    Tap,

    /// <summary>Key held at or beyond the threshold — dictate.</summary>
    Hold,
}

/// <summary>
/// Pure timing decision for the Flex-slot key: maps the key-down-to-key-up
/// duration to <see cref="TapOrHold"/> against a configurable threshold. No Win32
/// hook, no clock — the caller measures the duration and passes it in, so this is
/// fully unit-testable. The boundary is exact: a duration strictly below the
/// threshold is a <see cref="TapOrHold.Tap"/>; one at or beyond it is a
/// <see cref="TapOrHold.Hold"/>.
/// </summary>
public sealed class TapHoldClassifier
{
    /// <summary>Design default tap/hold boundary (design contract §2).</summary>
    public static readonly TimeSpan DefaultThreshold = TimeSpan.FromMilliseconds(250);

    private readonly TimeSpan _threshold;

    public TapHoldClassifier(TimeSpan? threshold = null)
    {
        _threshold = threshold ?? DefaultThreshold;
    }

    /// <summary>The boundary in effect, so callers/tests can surface or assert it.</summary>
    public TimeSpan Threshold => _threshold;

    /// <summary>
    /// Classifies how long the key was held: <see cref="TapOrHold.Tap"/> when
    /// <paramref name="heldDuration"/> is below the threshold,
    /// <see cref="TapOrHold.Hold"/> when it reaches or exceeds it.
    /// </summary>
    public TapOrHold Classify(TimeSpan heldDuration)
        => heldDuration < _threshold ? TapOrHold.Tap : TapOrHold.Hold;
}
