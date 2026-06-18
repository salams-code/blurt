using Blurt.Core;
using Xunit;

namespace Blurt.Core.Tests;

/// <summary>
/// The pure provider/key-application decision (issue 17 + security finding F1):
/// which key the refiner sends, and to which host. The OpenAI cloud needs the
/// stored key; a local/Ollama endpoint needs none — and switching to it must
/// never delete the stored key, only stop sending it. On top of that, the key is
/// only ever sent to OpenAI's own host, a loopback address, or a host the user
/// has explicitly trusted, so a tricked or tampered base URL can't silently
/// exfiltrate the key to a stranger.
/// </summary>
public class RefinerAuthTests
{
    private const string OpenAiUrl = "https://api.openai.com/v1";

    [Fact]
    public void OpenAi_with_a_stored_key_sends_it_to_the_openai_host()
    {
        var key = RefinerAuth.KeyToSend(RefinementProvider.OpenAi, "sk-test-123", OpenAiUrl, trustedHost: null);

        Assert.Equal("sk-test-123", key);
    }

    [Fact]
    public void OpenAi_with_no_stored_key_sends_an_empty_key()
    {
        // No key stored yet — nothing to send. The refiner then omits the
        // Authorization header and the pipeline falls back to the raw transcript.
        Assert.Equal(string.Empty, RefinerAuth.KeyToSend(RefinementProvider.OpenAi, null, OpenAiUrl, null));
        Assert.Equal(string.Empty, RefinerAuth.KeyToSend(RefinementProvider.OpenAi, "", OpenAiUrl, null));
    }

    [Fact]
    public void The_key_is_withheld_from_an_untrusted_custom_host()
    {
        // F1: the base URL was pointed at a stranger (a tricked "faster proxy" or a
        // tampered config.json) while the OpenAi provider is still selected. The key
        // must NOT travel there — withhold it (empty = no Authorization header).
        var key = RefinerAuth.KeyToSend(
            RefinementProvider.OpenAi, "sk-test-123", "https://evil.example/v1", trustedHost: null);

        Assert.Equal(string.Empty, key);
    }

    [Fact]
    public void The_key_is_sent_to_a_host_the_user_explicitly_trusted()
    {
        // A legitimate authenticated gateway (OpenRouter, a self-hosted proxy) the
        // user confirmed: once trusted, the key flows there.
        var key = RefinerAuth.KeyToSend(
            RefinementProvider.OpenAi, "sk-test-123", "https://openrouter.ai/api/v1", trustedHost: "openrouter.ai");

        Assert.Equal("sk-test-123", key);
    }

    [Fact]
    public void The_trusted_host_match_is_case_insensitive()
    {
        var key = RefinerAuth.KeyToSend(
            RefinementProvider.OpenAi, "sk-test-123", "https://OpenRouter.AI/api/v1", trustedHost: "openrouter.ai");

        Assert.Equal("sk-test-123", key);
    }

    [Fact]
    public void The_key_is_sent_to_a_loopback_host_without_an_explicit_trust_entry()
    {
        // A locally-hosted authenticated gateway is not exposed on the wire, so the
        // key can't leak in transit — loopback is trusted implicitly.
        var key = RefinerAuth.KeyToSend(
            RefinementProvider.OpenAi, "sk-test-123", "http://localhost:1234/v1", trustedHost: null);

        Assert.Equal("sk-test-123", key);
    }

    [Fact]
    public void A_malformed_base_url_withholds_the_key()
    {
        // If the host can't even be determined, default to safe: don't send the key.
        var key = RefinerAuth.KeyToSend(
            RefinementProvider.OpenAi, "sk-test-123", "not a url", trustedHost: null);

        Assert.Equal(string.Empty, key);
    }

    [Fact]
    public void Local_with_a_stored_key_does_not_send_it_but_it_stays_stored()
    {
        // The key persists (DPAPI, owned by the store) — KeyToSend only governs
        // what goes on the wire. For a local endpoint that is never the key, even
        // when one is stored, so switching providers keeps the key for later.
        var key = RefinerAuth.KeyToSend(
            RefinementProvider.LocalOpenAiCompatible, "sk-test-123", "http://localhost:11434/v1", null);

        Assert.Equal(string.Empty, key);
    }

    [Fact]
    public void Local_never_sends_a_key_even_to_the_openai_host()
    {
        var key = RefinerAuth.KeyToSend(
            RefinementProvider.LocalOpenAiCompatible, "sk-test-123", OpenAiUrl, trustedHost: null);

        Assert.Equal(string.Empty, key);
    }

    [Fact]
    public void The_default_provider_is_OpenAi_so_behaviour_is_unchanged()
    {
        // A config written before this setting existed deserialises with the enum
        // default (0 = OpenAi), so the stored key is still sent — today's behaviour.
        Assert.Equal(RefinementProvider.OpenAi, BlurtConfig.Default.RefinementProvider);
    }

    // --- UntrustedKeyHost: the settings UI uses this to ask for consent once ---

    [Theory]
    [InlineData(OpenAiUrl)]
    [InlineData("http://localhost:11434/v1")]
    [InlineData("http://127.0.0.1:1234/v1")]
    public void No_consent_is_needed_for_openai_or_loopback(string url)
    {
        Assert.Null(RefinerAuth.UntrustedKeyHost(RefinementProvider.OpenAi, url, trustedHost: null));
    }

    [Fact]
    public void No_consent_is_needed_for_an_already_trusted_host()
    {
        Assert.Null(RefinerAuth.UntrustedKeyHost(
            RefinementProvider.OpenAi, "https://openrouter.ai/api/v1", trustedHost: "openrouter.ai"));
    }

    [Fact]
    public void A_custom_untrusted_host_is_returned_so_the_ui_can_ask()
    {
        Assert.Equal(
            "evil.example",
            RefinerAuth.UntrustedKeyHost(RefinementProvider.OpenAi, "https://evil.example/v1", trustedHost: null));
    }

    [Fact]
    public void No_consent_is_needed_for_the_local_provider()
    {
        // The local provider never sends a key, so there is nothing to consent to.
        Assert.Null(RefinerAuth.UntrustedKeyHost(
            RefinementProvider.LocalOpenAiCompatible, "https://evil.example/v1", trustedHost: null));
    }

    [Fact]
    public void No_consent_is_asked_for_a_malformed_url()
    {
        // KeyToSend withholds the key for an unparseable URL anyway; there is no
        // host to ask about, so the UI is not prompted.
        Assert.Null(RefinerAuth.UntrustedKeyHost(RefinementProvider.OpenAi, "not a url", trustedHost: null));
    }
}
