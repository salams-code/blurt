using Blurt.Core;
using Xunit;

namespace Blurt.Core.Tests;

public class RefinementPromptsTests
{
    [Fact]
    public void Fix_prompt_instructs_a_german_cleanup_without_changing_meaning()
    {
        var prompt = RefinementPrompts.Fix;

        Assert.False(string.IsNullOrWhiteSpace(prompt));
        // German output is the contract for the Fix mode (design + issue 09).
        Assert.Contains("German", prompt, System.StringComparison.OrdinalIgnoreCase);
        // It is a cleanup prompt: grammar / punctuation / filler words.
        Assert.Contains("grammar", prompt, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("punctuation", prompt, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("filler", prompt, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void English_prompt_instructs_a_translation_into_english()
    {
        var prompt = RefinementPrompts.English;

        Assert.False(string.IsNullOrWhiteSpace(prompt));
        // English output is the contract for the English mode (design + issue 10).
        Assert.Contains("English", prompt, System.StringComparison.OrdinalIgnoreCase);
        // It is a translation prompt, not a cleanup.
        Assert.Contains("translat", prompt, System.StringComparison.OrdinalIgnoreCase);
    }
}
