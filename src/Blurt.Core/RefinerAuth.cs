namespace Blurt.Core;

/// <summary>
/// The pure provider/key-application decision (issue 17 + security finding F1):
/// given the active <see cref="RefinementProvider"/>, the stored key, and the
/// endpoint the refiner is about to call, decides which key actually goes on the
/// wire. This is the only place that gates the key — the store still owns
/// persistence (DPAPI), so a provider switch never deletes the stored key, it
/// just changes whether it is sent.
///
/// Two gates apply:
/// <list type="number">
///   <item>Provider: <see cref="RefinementProvider.OpenAi"/> sends the stored key;
///   a <see cref="RefinementProvider.LocalOpenAiCompatible"/> endpoint never does.</item>
///   <item>Host: even for OpenAi, the key only travels to OpenAI's own host, a
///   loopback address, or a host the user has explicitly trusted. A base URL that
///   was tricked or tampered onto a stranger gets no key, so the credential can't
///   be silently exfiltrated.</item>
/// </list>
/// Kept in Core so "when is the key sent?" is unit-tested offline, and the app
/// layer is a thin pass-through.
/// </summary>
public static class RefinerAuth
{
    /// <summary>OpenAI's own API host — always trusted to receive the key.</summary>
    private const string OpenAiHost = "api.openai.com";

    /// <summary>
    /// The API key to attach for <paramref name="provider"/> when calling
    /// <paramref name="baseUrl"/>. Returns the empty string ("no Authorization
    /// header") whenever the key must not be sent: a non-OpenAi provider, no stored
    /// key, an unparseable URL, or a host that is neither OpenAI, loopback, nor the
    /// user-trusted <paramref name="trustedHost"/>.
    /// </summary>
    public static string KeyToSend(
        RefinementProvider provider,
        string? storedKey,
        string baseUrl,
        string? trustedHost)
    {
        if (provider != RefinementProvider.OpenAi || string.IsNullOrEmpty(storedKey))
        {
            return string.Empty;
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
        {
            // Can't determine where the key would go — default to safe.
            return string.Empty;
        }

        return IsKeyHostTrusted(uri, trustedHost) ? storedKey : string.Empty;
    }

    /// <summary>
    /// The host that would receive the API key but has not been trusted yet, or
    /// <c>null</c> when no consent is needed: a non-OpenAi provider, an unparseable
    /// URL, or an already-trusted host (OpenAI, loopback, or
    /// <paramref name="trustedHost"/>). The settings UI uses this to ask the user
    /// once before letting the key travel to a custom host (security finding F1).
    /// </summary>
    public static string? UntrustedKeyHost(RefinementProvider provider, string baseUrl, string? trustedHost)
    {
        if (provider != RefinementProvider.OpenAi
            || !Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        return IsKeyHostTrusted(uri, trustedHost) ? null : uri.Host;
    }

    // The key may travel to OpenAI's own host, any loopback address (not exposed on
    // the wire), or a host the user explicitly trusted — nothing else.
    private static bool IsKeyHostTrusted(Uri uri, string? trustedHost) =>
        string.Equals(uri.Host, OpenAiHost, StringComparison.OrdinalIgnoreCase)
        || uri.IsLoopback
        || (!string.IsNullOrEmpty(trustedHost)
            && string.Equals(uri.Host, trustedHost, StringComparison.OrdinalIgnoreCase));
}
