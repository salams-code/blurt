using Blurt.Core;
using Xunit;

namespace Blurt.Core.Tests;

/// <summary>
/// The pure provider/key-application decision (issue 17): which key the refiner
/// sends for the active provider. The OpenAI cloud needs the stored key; a
/// local/Ollama endpoint needs none — and switching to it must never delete the
/// stored key, only stop sending it. So Local always yields an empty key while
/// the stored value stays put for when the user switches back.
/// </summary>
public class RefinerAuthTests
{
    [Fact]
    public void OpenAi_with_a_stored_key_sends_that_key()
    {
        var key = RefinerAuth.KeyToSend(RefinementProvider.OpenAi, "sk-test-123");

        Assert.Equal("sk-test-123", key);
    }

    [Fact]
    public void OpenAi_with_no_stored_key_sends_an_empty_key()
    {
        // No key stored yet — nothing to send. The refiner then omits the
        // Authorization header and the pipeline falls back to the raw transcript.
        Assert.Equal(string.Empty, RefinerAuth.KeyToSend(RefinementProvider.OpenAi, null));
        Assert.Equal(string.Empty, RefinerAuth.KeyToSend(RefinementProvider.OpenAi, ""));
    }

    [Fact]
    public void Local_with_a_stored_key_does_not_send_it_but_it_stays_stored()
    {
        // The key persists (DPAPI, owned by the store) — KeyToSend only governs
        // what goes on the wire. For a local endpoint that is never the key, even
        // when one is stored, so switching providers keeps the key for later.
        var key = RefinerAuth.KeyToSend(RefinementProvider.LocalOpenAiCompatible, "sk-test-123");

        Assert.Equal(string.Empty, key);
    }

    [Fact]
    public void Local_with_no_stored_key_sends_an_empty_key()
    {
        // A keyless local Ollama: empty in, empty out — no Authorization header.
        Assert.Equal(string.Empty, RefinerAuth.KeyToSend(RefinementProvider.LocalOpenAiCompatible, null));
        Assert.Equal(string.Empty, RefinerAuth.KeyToSend(RefinementProvider.LocalOpenAiCompatible, ""));
    }

    [Fact]
    public void The_default_provider_is_OpenAi_so_behaviour_is_unchanged()
    {
        // A config written before this setting existed deserialises with the enum
        // default (0 = OpenAi), so the stored key is still sent — today's behaviour.
        Assert.Equal(RefinementProvider.OpenAi, BlurtConfig.Default.RefinementProvider);
    }
}
