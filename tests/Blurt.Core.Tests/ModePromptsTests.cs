using Blurt.Core;
using Xunit;

namespace Blurt.Core.Tests;

/// <summary>
/// The per-mode prompt resolution behind issue 35: every refined mode (Fix,
/// English, Bullets, Custom) reads an editable prompt from config, falling back
/// to the shipped default. Pur is intentionally absent — it is verbatim and
/// promptless. These tests pin the defaults, the override, and the
/// blank-handling rules; the resolution is the single editable source the
/// Settings window and the dictation pipeline both go through.
/// </summary>
public class ModePromptsTests
{
    [Theory]
    [InlineData(RefinedMode.Fix)]
    [InlineData(RefinedMode.English)]
    [InlineData(RefinedMode.Bullets)]
    public void DefaultFor_an_always_on_mode_is_its_shipped_constant(RefinedMode mode)
    {
        // The three always-on refined modes ship with the RefinementPrompts
        // constants as their defaults, so an untouched install behaves as today.
        var expected = mode switch
        {
            RefinedMode.Fix => RefinementPrompts.Fix,
            RefinedMode.English => RefinementPrompts.English,
            RefinedMode.Bullets => RefinementPrompts.Bullets,
            _ => "",
        };

        Assert.Equal(expected, ModePrompts.DefaultFor(mode));
    }

    [Fact]
    public void DefaultFor_custom_is_empty_so_an_unset_custom_mode_has_no_prompt()
    {
        // Custom carries no built-in wording: until the user writes one it is empty,
        // and an empty prompt means "no refiner" (verbatim) — unchanged from today.
        Assert.Equal("", ModePrompts.DefaultFor(RefinedMode.Custom));
    }

    [Theory]
    [InlineData(RefinedMode.Fix)]
    [InlineData(RefinedMode.English)]
    [InlineData(RefinedMode.Bullets)]
    [InlineData(RefinedMode.Custom)]
    public void For_an_unedited_config_returns_the_default(RefinedMode mode)
    {
        // The default config resolves every mode to its shipped default — the
        // backward-compatibility contract (criterion 3).
        Assert.Equal(ModePrompts.DefaultFor(mode), ModePrompts.For(mode, BlurtConfig.Default));
    }

    [Fact]
    public void For_returns_the_user_override_when_one_is_set()
    {
        var config = BlurtConfig.Default with
        {
            FixPrompt = "Just fix the commas.",
            EnglishPrompt = "Translate to British English.",
            BulletsPrompt = "Make terse bullets.",
            CustomPrompt = "Speak like a pirate.",
        };

        Assert.Equal("Just fix the commas.", ModePrompts.For(RefinedMode.Fix, config));
        Assert.Equal("Translate to British English.", ModePrompts.For(RefinedMode.English, config));
        Assert.Equal("Make terse bullets.", ModePrompts.For(RefinedMode.Bullets, config));
        Assert.Equal("Speak like a pirate.", ModePrompts.For(RefinedMode.Custom, config));
    }

    [Theory]
    [InlineData(RefinedMode.Fix)]
    [InlineData(RefinedMode.English)]
    [InlineData(RefinedMode.Bullets)]
    public void A_blank_override_on_an_always_on_mode_falls_back_to_the_default(RefinedMode mode)
    {
        // Fix/English/Bullets always refine; blanking the field must not silently
        // disable the mode — it falls back to the shipped default instead.
        var config = mode switch
        {
            RefinedMode.Fix => BlurtConfig.Default with { FixPrompt = "   " },
            RefinedMode.English => BlurtConfig.Default with { EnglishPrompt = "" },
            RefinedMode.Bullets => BlurtConfig.Default with { BulletsPrompt = "\t\n" },
            _ => BlurtConfig.Default,
        };

        Assert.Equal(ModePrompts.DefaultFor(mode), ModePrompts.For(mode, config));
    }

    [Fact]
    public void A_blank_custom_prompt_stays_blank_so_custom_can_be_left_unset()
    {
        // Unlike the always-on modes, Custom may legitimately have no prompt — a
        // blank stays blank so the caller falls back to a verbatim (no-refiner)
        // dictation rather than substituting some default wording.
        var config = BlurtConfig.Default with { CustomPrompt = "   " };

        Assert.True(string.IsNullOrWhiteSpace(ModePrompts.For(RefinedMode.Custom, config)));
    }

    [Fact]
    public void Editing_one_mode_leaves_the_others_at_their_defaults()
    {
        // Per-mode independence: overriding Fix must not disturb English or Bullets.
        var config = BlurtConfig.Default with { FixPrompt = "Only touch Fix." };

        Assert.Equal("Only touch Fix.", ModePrompts.For(RefinedMode.Fix, config));
        Assert.Equal(RefinementPrompts.English, ModePrompts.For(RefinedMode.English, config));
        Assert.Equal(RefinementPrompts.Bullets, ModePrompts.For(RefinedMode.Bullets, config));
    }
}
