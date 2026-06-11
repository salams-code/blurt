using Blurt.Core;
using Xunit;

namespace Blurt.Core.Tests;

public class DictationPipelineTests
{
    [Fact]
    public async Task Transcribes_the_audio_and_injects_the_resulting_text_at_the_cursor()
    {
        var transcriber = new FakeTranscriber { Text = "hallo welt" };
        var injector = new RecordingInjector();
        var pipeline = new DictationPipeline(transcriber, injector);

        var outcome = await pipeline.RunAsync(Audio());

        Assert.Equal("hallo welt", injector.InjectedText);
        Assert.Equal(DictationOutcome.Injected, outcome);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public async Task An_empty_or_whitespace_transcript_injects_nothing(string transcript)
    {
        var transcriber = new FakeTranscriber { Text = transcript };
        var injector = new RecordingInjector();
        var pipeline = new DictationPipeline(transcriber, injector);

        var outcome = await pipeline.RunAsync(Audio());

        Assert.False(injector.WasCalled);
        Assert.Equal(DictationOutcome.NothingTranscribed, outcome);
    }

    [Fact]
    public async Task A_transcription_error_is_fail_soft_nothing_is_injected()
    {
        var transcriber = new FakeTranscriber { Throws = new InvalidOperationException("model busy") };
        var injector = new RecordingInjector();
        var pipeline = new DictationPipeline(transcriber, injector);

        var outcome = await pipeline.RunAsync(Audio());

        // Fail-soft: the error must not bubble out, and nothing may be injected.
        Assert.False(injector.WasCalled);
        Assert.Equal(DictationOutcome.TranscriptionFailed, outcome);
    }

    [Fact]
    public async Task A_refinement_step_runs_between_transcription_and_injection()
    {
        var transcriber = new FakeTranscriber { Text = "hallo welt" };
        var injector = new RecordingInjector();
        // Proves the refinement seam: a later mode can transform the text without
        // rewriting the pipeline. Here it upper-cases what Whisper produced.
        var pipeline = new DictationPipeline(
            transcriber,
            injector,
            refine: (text, _) => Task.FromResult(text.ToUpperInvariant()));

        var outcome = await pipeline.RunAsync(Audio());

        Assert.Equal("HALLO WELT", injector.InjectedText);
        Assert.Equal(DictationOutcome.Injected, outcome);
    }

    // --- hand-rolled fakes over the pipeline's seams ---

    private static Stream Audio() => new MemoryStream(new byte[] { 1, 2, 3 });

    private sealed class FakeTranscriber : ITranscriber
    {
        public string Text { get; init; } = "";
        public Exception? Throws { get; init; }

        public Task<string> TranscribeAsync(Stream wavAudio, CancellationToken ct = default)
            => Throws is null ? Task.FromResult(Text) : Task.FromException<string>(Throws);
    }

    private sealed class RecordingInjector : ITextInjector
    {
        public string? InjectedText { get; private set; }
        public bool WasCalled { get; private set; }
        public bool Result { get; init; } = true;

        public Task<bool> InjectAsync(string text, CancellationToken ct = default)
        {
            WasCalled = true;
            InjectedText = text;
            return Task.FromResult(Result);
        }
    }
}
