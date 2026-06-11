using Blurt.Core;
using Xunit;

namespace Blurt.Core.Tests;

public class SettingsValidationTests
{
    [Fact]
    public void The_default_config_is_valid()
    {
        var result = SettingsValidation.Validate(BlurtConfig.Default);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Two_triggers_on_the_same_key_is_a_conflict()
    {
        var config = BlurtConfig.Default with
        {
            HotkeyBindings = new Dictionary<TriggerKind, string>
            {
                [TriggerKind.Fix] = "AltGr+,",
                [TriggerKind.English] = "AltGr+,",   // collides with Fix
                [TriggerKind.FlexSlot] = "AltGr+-",
            },
        };

        var result = SettingsValidation.Validate(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("hotkey", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Distinct_remapped_hotkeys_are_not_a_conflict()
    {
        var config = BlurtConfig.Default with
        {
            HotkeyBindings = new Dictionary<TriggerKind, string>
            {
                [TriggerKind.Fix] = "AltGr+.",
                [TriggerKind.English] = "AltGr+-",
                [TriggerKind.FlexSlot] = "AltGr+,",
            },
        };

        var result = SettingsValidation.Validate(config);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a url")]
    [InlineData("ftp://example.com")]            // wrong scheme
    [InlineData("/v1")]                           // relative, not absolute
    [InlineData("example.com/v1")]                // missing scheme
    public void A_malformed_base_url_is_rejected(string url)
    {
        var config = BlurtConfig.Default with { RefinementBaseUrl = url };

        var result = SettingsValidation.Validate(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("URL", System.StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("https://api.openai.com/v1")]
    [InlineData("http://localhost:11434/v1")]
    public void An_absolute_http_or_https_base_url_is_accepted(string url)
    {
        var config = BlurtConfig.Default with { RefinementBaseUrl = url };

        var result = SettingsValidation.Validate(config);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Multiple_problems_are_all_reported_at_once()
    {
        var config = BlurtConfig.Default with
        {
            RefinementBaseUrl = "garbage",
            HotkeyBindings = new Dictionary<TriggerKind, string>
            {
                [TriggerKind.Fix] = "AltGr+,",
                [TriggerKind.English] = "AltGr+,",
                [TriggerKind.FlexSlot] = "AltGr+-",
            },
        };

        var result = SettingsValidation.Validate(config);

        Assert.False(result.IsValid);
        // Both the conflict and the bad URL surface, not just the first.
        Assert.True(result.Errors.Count >= 2);
    }
}
