namespace Blurt.Core;

/// <summary>
/// What one dictation attempt resulted in, so the caller can show the right
/// notice (or none). Distinct from a bare bool because the three non-injecting
/// cases differ in how they should be surfaced: nothing to say vs. a failure.
/// </summary>
public enum DictationOutcome
{
    /// <summary>The transcribed text was injected at the cursor.</summary>
    Injected,

    /// <summary>The transcript was empty/whitespace — nothing was injected.</summary>
    NothingTranscribed,

    /// <summary>Transcription threw — fail-soft, nothing injected.</summary>
    TranscriptionFailed,
}

/// <summary>
/// Owns the record → transcribe → inject sequence for one push-to-talk
/// utterance. "Pur" mode is verbatim Whisper output with zero network: the
/// optional <see cref="_refine"/> step is null, so nothing touches the text
/// between transcription and injection.
///
/// The refinement seam is a plain delegate rather than a baked-in interface so
/// a later mode (issue 09) can insert an LLM rewrite step without rewriting the
/// pipeline — it just constructs the pipeline with a non-null transform.
/// </summary>
public sealed class DictationPipeline
{
    private readonly ITranscriber _transcriber;
    private readonly ITextInjector _injector;
    private readonly Func<string, CancellationToken, Task<string>>? _refine;

    public DictationPipeline(
        ITranscriber transcriber,
        ITextInjector injector,
        Func<string, CancellationToken, Task<string>>? refine = null)
    {
        _transcriber = transcriber;
        _injector = injector;
        _refine = refine;
    }

    /// <summary>
    /// Runs one dictation over <paramref name="wavAudio"/> (16 kHz 16-bit mono
    /// PCM WAV). Does not dispose the stream — the caller owns its lifetime.
    /// Fail-soft throughout: a transcription error or an empty transcript means
    /// nothing is injected, signalled through the returned outcome rather than
    /// an exception.
    /// </summary>
    public async Task<DictationOutcome> RunAsync(Stream wavAudio, CancellationToken ct = default)
    {
        string text;
        try
        {
            text = await _transcriber.TranscribeAsync(wavAudio, ct);
        }
        catch
        {
            // Fail-soft (design §10): a failed transcription is a notice, not a
            // crash. The caller decides how loudly to surface it.
            return DictationOutcome.TranscriptionFailed;
        }

        // Refinement insertion point. Null in Pur mode (verbatim, zero network);
        // a later mode supplies a transform here without changing this method.
        if (_refine is not null)
        {
            text = await _refine(text, ct);
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return DictationOutcome.NothingTranscribed;
        }

        await _injector.InjectAsync(text, ct);
        return DictationOutcome.Injected;
    }
}
