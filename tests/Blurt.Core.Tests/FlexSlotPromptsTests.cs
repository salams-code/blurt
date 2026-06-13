using Blurt.Core;
using Xunit;

namespace Blurt.Core.Tests;

public class FlexSlotPromptsTests
{
    [Fact]
    public void Pur_resolves_to_no_refiner_prompt()
    {
        // Pur is the offline mode: there is no system prompt to send, so the
        // caller knows to skip the refiner entirely (zero network calls).
        var prompt = FlexSlotPrompts.For(FlexSlotMode.Pur, BlurtConfig.Default);

        Assert.True(string.IsNullOrEmpty(prompt));
    }

    [Fact]
    public void Bullets_resolves_to_the_shared_bullets_prompt()
    {
        var prompt = FlexSlotPrompts.For(FlexSlotMode.Bullets, BlurtConfig.Default);

        Assert.Equal(RefinementPrompts.Bullets, prompt);
    }

    [Fact]
    public void Custom_resolves_to_the_stored_custom_prompt_from_config()
    {
        // Custom carries no built-in constant — it reads the user-defined prompt
        // straight from settings, so two configs map to two different prompts.
        var config = BlurtConfig.Default with { CustomPrompt = "Translate into pirate speak." };

        var prompt = FlexSlotPrompts.For(FlexSlotMode.Custom, config);

        Assert.Equal("Translate into pirate speak.", prompt);
    }

    [Fact]
    public void Email_resolves_to_the_email_formation_prompt()
    {
        // Holding on Email must carry a prompt that turns conversational speech
        // into a well-formed email (issue 36, criterion 2) — never null, so the
        // caller runs the refiner rather than dropping to a verbatim transcript.
        var prompt = FlexSlotPrompts.For(FlexSlotMode.Email, BlurtConfig.Default);

        Assert.False(string.IsNullOrWhiteSpace(prompt));
        Assert.Contains("email", prompt!, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Custom_with_no_stored_prompt_resolves_to_no_refiner_prompt()
    {
        // An empty custom prompt has nothing to refine with; like Pur it resolves
        // to empty so the caller falls back to a raw (no-refiner) dictation.
        var config = BlurtConfig.Default with { CustomPrompt = "   " };

        var prompt = FlexSlotPrompts.For(FlexSlotMode.Custom, config);

        Assert.True(string.IsNullOrEmpty(prompt));
    }
}
