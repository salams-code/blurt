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
    public void Plain_http_for_the_OpenAi_provider_is_rejected_so_the_key_never_travels_in_clear_text()
    {
        // F4: the OpenAi provider attaches the stored API key as a Bearer header.
        // Over plain http that key (and the transcript) travel in clear text — a
        // MITM reads both. Reject it at the save gate.
        var config = BlurtConfig.Default with
        {
            RefinementProvider = RefinementProvider.OpenAi,
            RefinementBaseUrl = "http://api.example.com/v1",
        };

        var result = SettingsValidation.Validate(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("https", System.StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("http://localhost:11434/v1")]
    [InlineData("http://127.0.0.1:1234/v1")]
    public void Plain_http_on_loopback_is_accepted_even_for_the_OpenAi_provider(string url)
    {
        // A loopback endpoint is not exposed on the wire, so http carries no MITM
        // risk — a locally-hosted authenticated gateway stays usable.
        var config = BlurtConfig.Default with
        {
            RefinementProvider = RefinementProvider.OpenAi,
            RefinementBaseUrl = url,
        };

        var result = SettingsValidation.Validate(config);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Plain_http_for_the_local_provider_is_accepted_because_no_key_is_sent()
    {
        // The LocalOpenAiCompatible provider never attaches the key, so plain http
        // to a custom self-hosted box leaks no credential — it stays valid.
        var config = BlurtConfig.Default with
        {
            RefinementProvider = RefinementProvider.LocalOpenAiCompatible,
            RefinementBaseUrl = "http://my-llm-box.lan/v1",
        };

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
