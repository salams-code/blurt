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
    public void Bullets_prompt_instructs_a_bullet_point_reformat_in_the_input_language()
    {
        var prompt = RefinementPrompts.Bullets;

        Assert.False(string.IsNullOrWhiteSpace(prompt));
        // It reformats the transcript into bullet points.
        Assert.Contains("bullet", prompt, System.StringComparison.OrdinalIgnoreCase);
        // The input's language is preserved (not pinned to German like Fix).
        Assert.Contains("language", prompt, System.StringComparison.OrdinalIgnoreCase);
    }
}
