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
    public async Task A_cancelled_transcription_is_a_clean_outcome_not_a_failure()
    {
        // Issue 47: cancelling a dictation (the user releases push-to-talk to
        // abort, the token is tripped) is deliberate, not an error. The primary
        // transcriber throwing OperationCanceledException must return Cancelled —
        // distinct from TranscriptionFailed — and nothing may be injected.
        var transcriber = new FakeTranscriber { Throws = new OperationCanceledException() };
        var injector = new RecordingInjector();
        var pipeline = new DictationPipeline(transcriber, injector);

        var outcome = await pipeline.RunAsync(Audio());

        Assert.False(injector.WasCalled);
        Assert.Equal(DictationOutcome.Cancelled, outcome);
    }

    [Fact]
    public async Task A_cancelled_transcription_does_not_attempt_the_local_fallback()
    {
        // Cancel means abort, not degrade: even with a local fallback wired, a
        // cancelled primary transcription must NOT fall back to the local model
        // (that would resurrect a dictation the user chose to abandon). The
        // fallback is never invoked and the outcome is Cancelled.
        var online = new FakeTranscriber { Throws = new OperationCanceledException() };
        var injector = new RecordingInjector();
        var fallbackCalled = false;
        var pipeline = new DictationPipeline(
            online,
            injector,
            transcribeFallback: (_, _) =>
            {
                fallbackCalled = true;
                return Task.FromResult("lokal transkribiert");
            });

        var outcome = await pipeline.RunAsync(Audio());

        Assert.False(fallbackCalled);
        Assert.False(injector.WasCalled);
        Assert.Equal(DictationOutcome.Cancelled, outcome);
    }

    [Fact]
    public async Task A_cancelled_local_fallback_is_a_clean_outcome_not_a_failure()
    {
        // The online attempt failed for a real reason (no network) and the local
        // fallback was then cancelled mid-run. That cancellation is deliberate, so
        // the outcome is Cancelled — not TranscriptionFailed — and nothing is
        // injected.
        var online = new FakeTranscriber { Throws = new HttpRequestException("no network") };
        var injector = new RecordingInjector();
        var pipeline = new DictationPipeline(
            online,
            injector,
            transcribeFallback: (_, _) => Task.FromException<string>(
                new OperationCanceledException()));

        var outcome = await pipeline.RunAsync(Audio());

        Assert.False(injector.WasCalled);
        Assert.Equal(DictationOutcome.Cancelled, outcome);
    }

    [Fact]
    public async Task A_cancelled_refinement_is_a_clean_outcome_not_refined_offline()
    {
        // Issue 47: a refiner throwing OperationCanceledException means the user
        // aborted, not that refinement was unreachable. It must return Cancelled
        // (NOT RefinedOffline, which would inject the raw transcript) and nothing
        // may be injected.
        var transcriber = new FakeTranscriber { Text = "ähm hallo welt" };
        var injector = new RecordingInjector();
        var pipeline = new DictationPipeline(
            transcriber,
            injector,
            refine: (_, _) => Task.FromException<string>(new OperationCanceledException()));

        var outcome = await pipeline.RunAsync(Audio());

        Assert.False(injector.WasCalled);
        Assert.Equal(DictationOutcome.Cancelled, outcome);
    }

    [Fact]
    public async Task A_cancelled_dictation_does_not_mask_a_genuine_refiner_failure()
    {
        // Regression guard for issue 47: Cancelled must not become a catch-all
        // that swallows real errors. A genuine refiner failure (network down) must
        // STILL fall back to RefinedOffline and inject the raw transcript.
        var transcriber = new FakeTranscriber { Text = "ähm hallo welt" };
        var injector = new RecordingInjector();
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
    public async Task An_online_transcription_failure_falls_back_to_a_local_transcriber()
    {
        // Issue 30: Full Cloud + a network outage must not lose the dictation.
        // When the (online) transcriber throws and a local fallback is wired, the
        // pipeline transcribes locally instead and signals TranscribedOffline so
        // the caller can surface an "offline — local model" notice.
        var online = new FakeTranscriber { Throws = new HttpRequestException("no network") };
        var injector = new RecordingInjector();
        var pipeline = new DictationPipeline(
            online,
            injector,
            transcribeFallback: (_, _) => Task.FromResult("lokal transkribiert"));

        var outcome = await pipeline.RunAsync(Audio());

        Assert.Equal("lokal transkribiert", injector.InjectedText);
        Assert.Equal(DictationOutcome.TranscribedOffline, outcome);
    }

    [Fact]
    public async Task A_failing_local_fallback_is_fail_soft_and_reports_transcription_failed()
    {
        // Both the online attempt and the local fallback fail (e.g. no usable
        // local model). Fail-soft: nothing is injected, no exception escapes, and
        // the outcome is the same TranscriptionFailed as having no fallback at all.
        var online = new FakeTranscriber { Throws = new HttpRequestException("no network") };
        var injector = new RecordingInjector();
        var pipeline = new DictationPipeline(
            online,
            injector,
            transcribeFallback: (_, _) => Task.FromException<string>(
                new InvalidOperationException("local model failed to load")));

        var outcome = await pipeline.RunAsync(Audio());

        Assert.False(injector.WasCalled);
        Assert.Equal(DictationOutcome.TranscriptionFailed, outcome);
    }

    [Fact]
    public async Task The_local_fallback_transcript_still_runs_through_refinement()
    {
        // The offline fallback transcript is treated like any other: refinement
        // still runs over it. The outcome stays TranscribedOffline (the more
        // fundamental degradation) even though refinement succeeded here.
        var online = new FakeTranscriber { Throws = new HttpRequestException("no network") };
        var injector = new RecordingInjector();
        var pipeline = new DictationPipeline(
            online,
            injector,
            refine: (text, _) => Task.FromResult(text.ToUpperInvariant()),
            transcribeFallback: (_, _) => Task.FromResult("lokal"));

        var outcome = await pipeline.RunAsync(Audio());

        Assert.Equal("LOKAL", injector.InjectedText);
        Assert.Equal(DictationOutcome.TranscribedOffline, outcome);
    }

    [Fact]
    public async Task Fully_offline_both_transcription_and_refinement_fall_back_and_report_transcribed_offline()
    {
        // The realistic outage: the online transcription fails (→ local fallback)
        // and the refiner is then also unreachable (→ raw text). The local raw
        // text lands at the cursor; the single notice reports the more fundamental
        // TranscribedOffline rather than RefinedOffline.
        var online = new FakeTranscriber { Throws = new HttpRequestException("no network") };
        var injector = new RecordingInjector();
        var pipeline = new DictationPipeline(
            online,
            injector,
            refine: (_, _) => Task.FromException<string>(
                new HttpRequestException("refiner unreachable")),
            transcribeFallback: (_, _) => Task.FromResult("lokaler rohtext"));

        var outcome = await pipeline.RunAsync(Audio());

        Assert.Equal("lokaler rohtext", injector.InjectedText);
        Assert.Equal(DictationOutcome.TranscribedOffline, outcome);
    }

    [Fact]
    public async Task The_local_fallback_sees_the_full_audio_after_the_primary_consumed_the_stream()
    {
        // The online attempt reads the WAV before failing, leaving the stream at
        // its end. The local fallback must still see the whole utterance, so the
        // pipeline rewinds the (seekable) stream before retrying.
        var injector = new RecordingInjector();
        var pipeline = new DictationPipeline(
            new StreamDrainingTranscriber(),
            injector,
            transcribeFallback: async (wav, ct) =>
            {
                using var copy = new MemoryStream();
                await wav.CopyToAsync(copy, ct);
                return $"{copy.Length} bytes";
            });

        var outcome = await pipeline.RunAsync(Audio());

        Assert.Equal("3 bytes", injector.InjectedText);
        Assert.Equal(DictationOutcome.TranscribedOffline, outcome);
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

    [Fact]
    public async Task The_final_text_is_reported_to_the_result_sink()
    {
        // Issue 26: the tray history records what a dictation produced. The sink
        // gets the *final* (post-refinement) text — what actually went to the
        // cursor — not the raw transcript.
        var transcriber = new FakeTranscriber { Text = "hallo welt" };
        var injector = new RecordingInjector();
        string? recorded = null;
        var pipeline = new DictationPipeline(
            transcriber,
            injector,
            refine: (text, _) => Task.FromResult(text.ToUpperInvariant()),
            onResult: text => recorded = text);

        await pipeline.RunAsync(Audio());

        Assert.Equal("HALLO WELT", recorded);
    }

    [Fact]
    public async Task A_blocked_paste_still_reports_the_text_to_the_result_sink()
    {
        // InjectionBlocked leaves the text on the clipboard — but the clipboard
        // is volatile, so the history is exactly the recovery net this case needs.
        var transcriber = new FakeTranscriber { Text = "hallo welt" };
        var injector = new RecordingInjector { Result = false };
        string? recorded = null;
        var pipeline = new DictationPipeline(
            transcriber, injector, onResult: text => recorded = text);

        await pipeline.RunAsync(Audio());

        Assert.Equal("hallo welt", recorded);
    }

    [Fact]
    public async Task Nothing_is_reported_when_nothing_was_transcribed()
    {
        var transcriber = new FakeTranscriber { Text = "[BLANK_AUDIO]" };
        var injector = new RecordingInjector();
        string? recorded = null;
        var pipeline = new DictationPipeline(
            transcriber, injector, onResult: text => recorded = text);

        await pipeline.RunAsync(Audio());

        Assert.Null(recorded);
    }

    // --- hand-rolled fakes over the pipeline's seams ---

    [Fact]
    public async Task Cancel_after_transcription_but_before_injection_injects_nothing_and_returns_Cancelled()
    {
        // Issue 48: the user pressed Esc once the (short) decode had finished but
        // before injection — the transcriber returned normally, never observing the
        // token. A tripped token here is still a deliberate abort: return Cancelled
        // and inject nothing, rather than letting the just-finished text land.
        var injector = new RecordingInjector();
        var pipeline = new DictationPipeline(
            new FakeTranscriber { Text = "hallo welt" }, injector);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var outcome = await pipeline.RunAsync(Audio(), cts.Token);

        Assert.Equal(DictationOutcome.Cancelled, outcome);
        Assert.False(injector.WasCalled);
    }

    [Fact]
    public async Task Untrusted_refiner_output_is_sanitised_before_it_reaches_the_clipboard_and_history()
    {
        // Security F2: a malicious/compromised refiner returns text carrying a
        // terminal escape and a trailing newline. Both must be neutralised before
        // the text hits the clipboard (paste) and the recovery history.
        var esc = (char)0x1b;
        var transcriber = new FakeTranscriber { Text = "hallo" };
        var injector = new RecordingInjector();
        string? recorded = null;
        var pipeline = new DictationPipeline(
            transcriber,
            injector,
            refine: (_, _) => Task.FromResult($"echo hi{esc}\nrm -rf ~\n"),
            onResult: text => recorded = text);

        var outcome = await pipeline.RunAsync(Audio());

        Assert.Equal("echo hi\nrm -rf ~", injector.InjectedText);
        Assert.Equal("echo hi\nrm -rf ~", recorded);
        Assert.Equal(DictationOutcome.Injected, outcome);
    }

    private static Stream Audio() => new MemoryStream(new byte[] { 1, 2, 3 });

    private sealed class FakeTranscriber : ITranscriber
    {
        public string Text { get; init; } = "";
        public Exception? Throws { get; init; }

        public Task<string> TranscribeAsync(Stream wavAudio, CancellationToken ct = default)
            => Throws is null ? Task.FromResult(Text) : Task.FromException<string>(Throws);
    }

    // Reads the WAV to its end (like a real network/local transcriber would)
    // before failing, so a fallback only sees the full audio if the pipeline
    // rewinds the stream.
    private sealed class StreamDrainingTranscriber : ITranscriber
    {
        public async Task<string> TranscribeAsync(Stream wavAudio, CancellationToken ct = default)
        {
            using var sink = new MemoryStream();
            await wavAudio.CopyToAsync(sink, ct);
            throw new HttpRequestException("no network");
        }
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
