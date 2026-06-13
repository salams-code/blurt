using Blurt.Core;
using Xunit;

namespace Blurt.Core.Tests;

public class PrivacyTiersTests
{
    [Fact]
    public void Fully_local_tier_keeps_audio_and_text_on_the_machine()
    {
        // Issue 27 Stufe 0: nothing leaves — local whisper.cpp transcription and a
        // local (Ollama) refiner. This is the "own your voice", fully-offline tier.
        var (transcription, refinement) = PrivacyTiers.SettingsFor(PrivacyTier.FullyLocal);

        Assert.Equal(TranscriptionMode.Local, transcription);
        Assert.Equal(RefinementProvider.LocalOpenAiCompatible, refinement);
    }

    [Fact]
    public void Voice_stays_home_tier_transcribes_locally_but_refines_in_the_cloud()
    {
        // Issue 27 Stufe 1, the "own your voice" sweet spot: the audio never leaves
        // (local transcription), only the transcribed text is sent to OpenAI.
        var (transcription, refinement) = PrivacyTiers.SettingsFor(PrivacyTier.VoiceStaysHome);

        Assert.Equal(TranscriptionMode.Local, transcription);
        Assert.Equal(RefinementProvider.OpenAi, refinement);
    }

    [Fact]
    public void Full_cloud_tier_sends_both_audio_and_text_to_openai()
    {
        // Issue 27 Stufe 2: fastest/best transcription, audio leaves the machine.
        var (transcription, refinement) = PrivacyTiers.SettingsFor(PrivacyTier.FullCloud);

        Assert.Equal(TranscriptionMode.Online, transcription);
        Assert.Equal(RefinementProvider.OpenAi, refinement);
    }

    [Fact]
    public void Classify_recognises_a_standard_combination_as_its_tier()
    {
        // The reverse direction: an existing config of local transcription + OpenAI
        // refinement is exactly the "voice stays home" tier, so the selector shows
        // Stufe 1 rather than "Custom".
        Assert.Equal(
            PrivacyTier.VoiceStaysHome,
            PrivacyTiers.Classify(TranscriptionMode.Local, RefinementProvider.OpenAi));
    }

    [Theory]
    [InlineData(PrivacyTier.FullyLocal)]
    [InlineData(PrivacyTier.VoiceStaysHome)]
    [InlineData(PrivacyTier.FullCloud)]
    public void Every_tiers_settings_classify_back_to_that_tier(PrivacyTier tier)
    {
        // Forward then reverse is the identity for every defined tier — so toggling
        // the selector and reloading shows the same tier, never a spurious "Custom".
        var (transcription, refinement) = PrivacyTiers.SettingsFor(tier);

        Assert.Equal(tier, PrivacyTiers.Classify(transcription, refinement));
    }

    [Fact]
    public void Classify_treats_a_non_standard_combination_as_custom()
    {
        // Online transcription with a local refiner is a deliberate "advanced"
        // combo that no tier represents — Classify returns null so the selector
        // shows "Custom" instead of mislabelling it as a tier.
        Assert.Null(PrivacyTiers.Classify(
            TranscriptionMode.Online, RefinementProvider.LocalOpenAiCompatible));
    }
}
