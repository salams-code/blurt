using System.Collections.Generic;
using Blurt.Core;
using Xunit;

namespace Blurt.Core.Tests;

public class ProviderEndpointsTests
{
    [Fact]
    public void Switching_to_a_provider_with_nothing_remembered_yields_its_defaults()
    {
        // Issue 24: first switch to Local/Ollama on a config that has never seen
        // it → the fields show the Ollama defaults, not the lingering OpenAI URL.
        var current = new RefinementEndpoint
        {
            BaseUrl = "https://api.openai.com/v1",
            Model = "gpt-4o-mini",
        };

        var (target, _) = ProviderEndpoints.Switch(
            from: RefinementProvider.OpenAi,
            current: current,
            to: RefinementProvider.LocalOpenAiCompatible,
            remembered: new Dictionary<RefinementProvider, RefinementEndpoint>());

        Assert.Equal("http://localhost:11434/v1", target.BaseUrl);
        Assert.Equal("llama3.1", target.Model);
    }

    [Fact]
    public void Switching_away_and_back_restores_the_edited_endpoint()
    {
        // The user's custom OpenAI-side values must survive a round trip through
        // Local — "remember each provider's own base URL + model" is the issue's
        // preferred design, explicitly not overwrite-with-defaults.
        var customOpenAi = new RefinementEndpoint
        {
            BaseUrl = "https://eu.proxy.example/v1",
            Model = "gpt-4.1",
        };

        var (local, remembered) = ProviderEndpoints.Switch(
            RefinementProvider.OpenAi, customOpenAi,
            RefinementProvider.LocalOpenAiCompatible,
            new Dictionary<RefinementProvider, RefinementEndpoint>());

        var (backToOpenAi, _) = ProviderEndpoints.Switch(
            RefinementProvider.LocalOpenAiCompatible, local,
            RefinementProvider.OpenAi,
            remembered);

        Assert.Equal(customOpenAi, backToOpenAi);
    }

    [Fact]
    public void The_returned_map_carries_both_providers_latest_values()
    {
        // What Switch returns is what gets persisted: after one switch the map
        // must already hold the edited source endpoint AND the target's values,
        // so a save at any point writes a complete per-provider memory.
        var editedLocal = new RefinementEndpoint
        {
            BaseUrl = "http://nas:11434/v1",
            Model = "qwen2.5",
        };

        var (_, remembered) = ProviderEndpoints.Switch(
            RefinementProvider.LocalOpenAiCompatible, editedLocal,
            RefinementProvider.OpenAi,
            new Dictionary<RefinementProvider, RefinementEndpoint>());

        Assert.Equal(editedLocal, remembered[RefinementProvider.LocalOpenAiCompatible]);
    }
}
