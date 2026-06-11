namespace Blurt.Core;

/// <summary>
/// The pure provider/key-application decision (issue 17): given the active
/// <see cref="RefinementProvider"/> and the key the store holds, decides which key
/// the refiner should actually send. This is the only place that gates the key on
/// the wire — the store still owns persistence (DPAPI), so a provider switch never
/// deletes the stored key, it just changes whether it is sent. Kept in Core so
/// "when is the key sent?" is unit-tested offline, and the app layer is a thin
/// pass-through.
/// </summary>
public static class RefinerAuth
{
    /// <summary>
    /// The API key to attach for <paramref name="provider"/>. For
    /// <see cref="RefinementProvider.OpenAi"/> the <paramref name="storedKey"/> is
    /// sent (empty when none is stored). For
    /// <see cref="RefinementProvider.LocalOpenAiCompatible"/> the result is always
    /// empty — a local/Ollama endpoint needs no auth — even when a key is stored,
    /// so the stored key survives the switch but is not sent. The empty string maps
    /// to "no Authorization header" in <see cref="OpenAiCompatibleRefiner"/>.
    /// </summary>
    public static string KeyToSend(RefinementProvider provider, string? storedKey) =>
        provider == RefinementProvider.OpenAi
            ? storedKey ?? string.Empty
            : string.Empty;
}
