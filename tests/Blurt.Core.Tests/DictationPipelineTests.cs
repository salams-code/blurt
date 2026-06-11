using System.Net.Http;
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

    [Theory]
    [InlineData("[BLANK_AUDIO]")]
    [InlineData("(Musik)")]
    [InlineData("[MUSIC]")]
    [InlineData("  (Musik)\n")]
    public async Task A_whisper_non_speech_marker_injects_nothing(string transcript)
    {
        // Whisper does not return an empty string for silence/noise — it emits a
        // bracketed annotation like "[BLANK_AUDIO]" or "(Musik)". Injecting that
        // literally at the cursor would be wrong; it means "no speech", same as
        // an empty transcript.
        var transcriber = new FakeTranscriber { Text = transcript };
        var injector = new RecordingInjector();
        var pipeline = new DictationPipeline(transcriber, injector);

        var outcome = await pipeline.RunAsync(Audio());

        Assert.False(injector.WasCalled);
        Assert.Equal(DictationOutcome.NothingTranscribed, outcome);
    }

    [Fact]
    public async Task Real_speech_containing_a_parenthetical_is_injected_verbatim()
    {
        // The non-speech guard must not mangle genuine dictation that happens to
        // contain brackets: only a transcript that is *entirely* annotation counts
        // as silence. Real words alongside it are injected unchanged.
        var transcriber = new FakeTranscriber { Text = "Ich gehe (heute) einkaufen" };
        var injector = new RecordingInjector();
        var pipeline = new DictationPipeline(transcriber, injector);

        var outcome = await pipeline.RunAsync(Audio());

        Assert.Equal("Ich gehe (heute) einkaufen", injector.InjectedText);
        Assert.Equal(DictationOutcome.Injected, outcome);
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

    [Fact]
    public async Task A_failing_refinement_falls_back_to_injecting_the_raw_transcript()
    {
        var transcriber = new FakeTranscriber { Text = "ähm hallo welt" };
        var injector = new RecordingInjector();
        // The refiner endpoint is unreachable: refinement must not lose the
        // dictation. The raw transcript is injected and the outcome says so, so
        // the caller can surface a "refinement offline" notice.
        var pipeline = new DictationPipeline(
            transcriber,
            injector,
            refine: (_, _) => Task.FromException<string>(
                new HttpRequestException("connection refused")));

        var outcome = await pipeline.RunAsync(Audio());

        Assert.Equal("ähm hallo welt", injector.InjectedText);
        Assert.Equal(DictationOutcome.RefinedOffline, outcome);
    }

    [Fact]
    public async Task A_blocked_paste_reports_injection_blocked_with_the_text_left_on_the_clipboard()
    {
        var transcriber = new FakeTranscriber { Text = "hallo welt" };
        // The target app refused the paste (InjectAsync returned false). The
        // injector already left the text on the clipboard, so nothing is lost —
        // the pipeline must surface that as InjectionBlocked, not Injected.
        var injector = new RecordingInjector { Result = false };
        var pipeline = new DictationPipeline(transcriber, injector);

        var outcome = await pipeline.RunAsync(Audio());

        Assert.True(injector.WasCalled);   // injection was attempted; text is on the clipboard
        Assert.Equal("hallo welt", injector.InjectedText);
        Assert.Equal(DictationOutcome.InjectionBlocked, outcome);
    }

    [Fact]
    public async Task A_blocked_paste_after_a_failed_refinement_still_reports_injection_blocked()
    {
        // Two things go wrong: the refiner is offline (raw transcript is used)
        // and the paste is then blocked. The injection failure dominates, because
        // the user-visible problem is the text not landing — it sits on the
        // clipboard, so "couldn't paste" is the notice that matters.
        var transcriber = new FakeTranscriber { Text = "ähm hallo welt" };
        var injector = new RecordingInjector { Result = false };
        var pipeline = new DictationPipeline(
            transcriber,
            injector,
            refine: (_, _) => Task.FromException<string>(
                new HttpRequestException("connection refused")));

        var outcome = await pipeline.RunAsync(Audio());

        Assert.Equal("ähm hallo welt", injector.InjectedText);   // raw transcript, on the clipboard
        Assert.Equal(DictationOutcome.InjectionBlocked, outcome);
    }

    [Fact]
    public async Task A_failing_refinement_on_a_non_speech_transcript_still_injects_nothing()
    {
        // If there was no speech to begin with, a refiner failure must not cause
        // a bracketed non-speech marker to be injected as the "raw" fallback.
        var transcriber = new FakeTranscriber { Text = "[BLANK_AUDIO]" };
        var injector = new RecordingInjector();
        var pipeline = new DictationPipeline(
            transcriber,
            injector,
            refine: (_, _) => Task.FromException<string>(
                new HttpRequestException("connection refused")));

        var outcome = await pipeline.RunAsync(Audio());

        Assert.False(injector.WasCalled);
        Assert.Equal(DictationOutcome.NothingTranscribed, outcome);
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
