namespace Blurt.Core;

/// <summary>
/// A guided privacy choice (issue 27), framed by the real trust boundary —
/// <b>what leaves the machine</b> — rather than the two technical axes
/// (transcription source, refinement provider) it maps onto. Pur stays
/// zero-network by contract regardless of the selected tier; this governs only
/// the refined modes.
/// </summary>
public enum PrivacyTier
{
    /// <summary>Stufe 0: audio and text both stay local — nothing leaves.</summary>
    FullyLocal,

    /// <summary>Stufe 1: voice stays home (local transcription), only the text is refined in the cloud.</summary>
    VoiceStaysHome,

    /// <summary>Stufe 2: full cloud — audio and text both go to OpenAI.</summary>
    FullCloud,
}

/// <summary>
/// Pure mapping between a <see cref="PrivacyTier"/> and the underlying
/// <see cref="TranscriptionMode"/> + <see cref="RefinementProvider"/> pair the
/// app actually runs on. The settings UI is the thin shell over this.
/// </summary>
public static class PrivacyTiers
{
    /// <summary>The concrete transcription source + refinement provider for a tier.</summary>
    public static (TranscriptionMode Transcription, RefinementProvider Refinement) SettingsFor(PrivacyTier tier) =>
        tier switch
        {
            PrivacyTier.FullyLocal => (TranscriptionMode.Local, RefinementProvider.LocalOpenAiCompatible),
            PrivacyTier.VoiceStaysHome => (TranscriptionMode.Local, RefinementProvider.OpenAi),
            PrivacyTier.FullCloud => (TranscriptionMode.Online, RefinementProvider.OpenAi),
            _ => throw new System.ArgumentOutOfRangeException(nameof(tier), tier, "Unknown privacy tier."),
        };

    /// <summary>
    /// The tier a transcription/refinement pair corresponds to, or <c>null</c>
    /// when the combination is non-standard (e.g. online transcription with a
    /// local refiner) — the UI surfaces that as "Custom".
    /// </summary>
    public static PrivacyTier? Classify(TranscriptionMode transcription, RefinementProvider refinement) =>
        (transcription, refinement) switch
        {
            (TranscriptionMode.Local, RefinementProvider.LocalOpenAiCompatible) => PrivacyTier.FullyLocal,
            (TranscriptionMode.Local, RefinementProvider.OpenAi) => PrivacyTier.VoiceStaysHome,
            (TranscriptionMode.Online, RefinementProvider.OpenAi) => PrivacyTier.FullCloud,
            _ => null,
        };
}
