using Blurt.Core;
using Xunit;

namespace Blurt.Core.Tests;

public class OnboardingTests
{
    [Fact]
    public void Onboarding_is_needed_for_a_fresh_default_config()
    {
        // A freshly-installed machine has no config.json, so SettingsStore.Load()
        // returns BlurtConfig.Default (OnboardingCompleted = false) and the guided
        // first-run flow must run.
        Assert.True(Onboarding.IsNeeded(BlurtConfig.Default));
    }

    [Fact]
    public void Onboarding_is_not_needed_once_it_has_been_completed()
    {
        // Once the wizard finishes it persists OnboardingCompleted = true; the flow
        // must never run again — even if the user skipped the optional API key.
        var completed = BlurtConfig.Default with { OnboardingCompleted = true };

        Assert.False(Onboarding.IsNeeded(completed));
    }

    [Fact]
    public void The_completion_flag_is_the_single_source_of_truth()
    {
        // A user who filled in everything but never reached "Finish" is still
        // considered to need onboarding — the flag, not the presence of other
        // settings, decides.
        var configuredButNotCompleted = BlurtConfig.Default with
        {
            RefinementModel = "gpt-4o",
            CustomPrompt = "Make it formal.",
            OnboardingCompleted = false,
        };

        Assert.True(Onboarding.IsNeeded(configuredButNotCompleted));
    }

    [Fact]
    public void Steps_run_in_the_microphone_apikey_model_hotkeys_order()
    {
        // The wizard walks these four steps in declaration order; assert it so a
        // reorder is a deliberate, test-visible change.
        Assert.Equal(
            new[]
            {
                OnboardingStep.Microphone,
                OnboardingStep.ApiKey,
                OnboardingStep.Model,
                OnboardingStep.Hotkeys,
            },
            Enum.GetValues<OnboardingStep>());
    }
}
