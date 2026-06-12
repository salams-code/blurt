using Blurt.Core;
using Xunit;

namespace Blurt.Core.Tests;

public class TranscriberResolverTests
{
    [Fact]
    public async Task Local_mode_resolves_the_local_transcriber()
    {
        var local = new StubTranscriber();
        var online = new StubTranscriber();

        var resolved = await TranscriberResolver.ResolveAsync(
            TranscriptionMode.Local, zeroNetwork: false,
            local: () => Task.FromResult<ITranscriber>(local),
            online: () => online);

        Assert.Same(local, resolved);
    }

    [Fact]
    public async Task Online_mode_resolves_the_online_transcriber_without_touching_local()
    {
        // The local factory provisions a multi-hundred-MB model on first use —
        // with Online selected it must not even be invoked.
        var online = new StubTranscriber();
        var localInvoked = false;

        var resolved = await TranscriberResolver.ResolveAsync(
            TranscriptionMode.Online, zeroNetwork: false,
            local: () => { localInvoked = true; return Task.FromResult<ITranscriber>(new StubTranscriber()); },
            online: () => online);

        Assert.Same(online, resolved);
        Assert.False(localInvoked);
    }

    [Fact]
    public async Task Zero_network_dictation_stays_local_even_when_online_is_configured()
    {
        // Pur's promise (design contract): verbatim dictation never touches the
        // network. Online transcription is simply not available to it — the
        // resolver enforces the guarantee, not the call sites' discipline.
        var local = new StubTranscriber();
        var onlineInvoked = false;

        var resolved = await TranscriberResolver.ResolveAsync(
            TranscriptionMode.Online, zeroNetwork: true,
            local: () => Task.FromResult<ITranscriber>(local),
            online: () => { onlineInvoked = true; return new StubTranscriber(); });

        Assert.Same(local, resolved);
        Assert.False(onlineInvoked);
    }

    private sealed class StubTranscriber : ITranscriber
    {
        public Task<string> TranscribeAsync(Stream wavAudio, CancellationToken ct = default)
            => Task.FromResult("");
    }
}
