using Blurt.Core;
using Xunit;

namespace Blurt.Core.Tests;

/// <summary>
/// The pure compose/translate decision behind the "also translate to English"
/// modifier (issue 39): layered on top of a mode's own refinement, per-dictation,
/// and a strict no-op on the verbatim path so Pur stays zero-network.
/// </summary>
public class TranslationModifierTests
{
    [Fact]
    public void Without_the_modifier_the_prompt_is_unchanged()
    {
        var basePrompt = "Reformat the transcript into bullet points.";

        Assert.Equal(basePrompt, TranslationModifier.Compose(basePrompt, alsoTranslate: false));
    }

    [Fact]
    public void With_the_modifier_the_english_layer_is_added_on_top_of_the_base_prompt()
    {
        var basePrompt = "Reformat the transcript into bullet points.";

        var composed = TranslationModifier.Compose(basePrompt, alsoTranslate: true);

        Assert.NotNull(composed);
        // The base mode's instruction is preserved (the English step is layered on top,
        // not a replacement), so Bullets stays bullets — just in English.
        Assert.StartsWith(basePrompt, composed);
        Assert.Contains("English", composed, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_modifier_layers_english_on_bullets_and_on_email()
    {
        // Criterion 1 names Bullets and Email explicitly: each mode's own instruction
        // is kept (Bullets stays bullets, Email stays an email) with English on top.
        foreach (var basePrompt in new[] { RefinementPrompts.Bullets, RefinementPrompts.Email })
        {
            var composed = TranslationModifier.Compose(basePrompt, alsoTranslate: true);

            Assert.NotNull(composed);
            Assert.StartsWith(basePrompt, composed);
            Assert.Contains("English", composed, System.StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void A_verbatim_path_stays_verbatim_so_the_modifier_never_touches_Pur()
    {
        // Null / blank base prompt = the verbatim path (Pur, or a Custom mode with no
        // prompt). Even with the modifier it stays null, so the caller dictates raw and
        // makes no network call.
        Assert.Null(TranslationModifier.Compose(null, alsoTranslate: true));
        Assert.True(string.IsNullOrWhiteSpace(TranslationModifier.Compose("   ", alsoTranslate: true)));
    }
}
