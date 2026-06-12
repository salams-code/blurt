using System.Collections.Generic;

namespace Blurt.Core;

/// <summary>One refinement provider's endpoint address + model name (issue 24).</summary>
public sealed record RefinementEndpoint
{
    public string BaseUrl { get; init; } = "";
    public string Model { get; init; } = "";
}

/// <summary>
/// Pure per-provider endpoint memory (issue 24): the two refinement providers
/// are two separate paths, so switching between them swaps in the target
/// provider's remembered (or default) base URL + model instead of leaving the
/// other provider's values sitting in the fields. The settings UI is the thin
/// shell over <see cref="Switch"/>.
/// </summary>
public static class ProviderEndpoints
{
    /// <summary>The out-of-the-box endpoint for <paramref name="provider"/>.</summary>
    public static RefinementEndpoint DefaultFor(RefinementProvider provider) =>
        provider == RefinementProvider.LocalOpenAiCompatible
            ? new RefinementEndpoint { BaseUrl = "http://localhost:11434/v1", Model = "llama3.1" }
            : new RefinementEndpoint { BaseUrl = "https://api.openai.com/v1", Model = "gpt-4o-mini" };

    /// <summary>
    /// Switch the active provider: remembers <paramref name="current"/> under
    /// <paramref name="from"/> and yields the endpoint to show for
    /// <paramref name="to"/> — its remembered values, or its defaults when it was
    /// never configured. The returned map carries both providers' latest values,
    /// ready to persist.
    /// </summary>
    public static (RefinementEndpoint Target, IReadOnlyDictionary<RefinementProvider, RefinementEndpoint> Remembered)
        Switch(
            RefinementProvider from,
            RefinementEndpoint current,
            RefinementProvider to,
            IReadOnlyDictionary<RefinementProvider, RefinementEndpoint> remembered)
    {
        var updated = new Dictionary<RefinementProvider, RefinementEndpoint>(remembered)
        {
            [from] = current,
        };

        var target = updated.TryGetValue(to, out var stored) ? stored : DefaultFor(to);
        return (target, updated);
    }
}
