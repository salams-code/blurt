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

    /// <summary>
    /// The refiner endpoint was unreachable (or otherwise failed), so the raw
    /// transcript was injected instead of the refined text. Fail-soft: the
    /// dictation still lands at the cursor; the caller surfaces a "refinement
    /// offline" notice.
    /// </summary>
    RefinedOffline,

    /// <summary>
    /// The paste keystroke was refused by the focused app, so the text could not
    /// be inserted at the cursor. Fail-soft: the injector leaves the text on the
    /// clipboard so it is never lost; the caller surfaces a "couldn't paste —
    /// text left on clipboard" notice so the user can paste it manually.
    /// </summary>
    InjectionBlocked,
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

        // A non-speech transcript is silence regardless of refinement — guard
        // before the network round-trip so an empty utterance never reaches the
        // refiner and a refiner failure can't resurrect a "[BLANK_AUDIO]" marker
        // as the raw fallback below.
        if (IsNonSpeech(text))
        {
            return DictationOutcome.NothingTranscribed;
        }

        // Refinement insertion point. Null in Pur mode (verbatim, zero network);
        // a later mode supplies a transform here without changing this method.
        // Fail-soft: if the refiner is unreachable, inject the raw transcript
        // rather than losing the dictation, and signal it so the caller can
        // surface a "refinement offline" notice.
        var refinedOffline = false;
        if (_refine is not null)
        {
            try
            {
                text = await _refine(text, ct);
            }
            catch
            {
                refinedOffline = true;
            }
        }

        // A false return means the paste was blocked by the focused app — the
        // injector has left the text on the clipboard, so nothing is lost. This
        // dominates RefinedOffline: the user-visible problem is the text not
        // landing at the cursor, not which version was attempted.
        var injected = await _injector.InjectAsync(text, ct);
        if (!injected)
        {
            return DictationOutcome.InjectionBlocked;
        }

        return refinedOffline ? DictationOutcome.RefinedOffline : DictationOutcome.Injected;
    }

    // Whisper never returns an empty string for silence or background noise; it
    // emits a bracketed annotation instead ("[BLANK_AUDIO]", "(Musik)",
    // "[MUSIC]"…). A transcript that is *entirely* such annotations (plus
    // whitespace) carries no spoken words, so it must not be injected. Genuine
    // dictation that merely contains a parenthetical keeps real words outside the
    // brackets, so the residue is non-empty and the original text is injected
    // verbatim — the annotations are never stripped from real speech.
    private static bool IsNonSpeech(string text)
    {
        var residue = NonSpeechAnnotation.Replace(text, string.Empty);
        return string.IsNullOrWhiteSpace(residue);
    }

    private static readonly System.Text.RegularExpressions.Regex NonSpeechAnnotation =
        new(@"\[[^\]]*\]|\([^)]*\)", System.Text.RegularExpressions.RegexOptions.Compiled);
}
