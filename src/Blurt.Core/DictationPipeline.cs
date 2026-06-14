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
    /// Online transcription failed (e.g. the network was down), so the audio was
    /// transcribed by a local whisper model instead and the result injected.
    /// Fail-soft (issue 30): Full Cloud degrades gracefully offline rather than
    /// losing the dictation; the caller surfaces a "transcribed locally (offline)"
    /// notice. Mirrors <see cref="RefinedOffline"/> for the transcription step.
    /// </summary>
    TranscribedOffline,

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

    /// <summary>
    /// The dictation was deliberately cancelled (the push-to-talk token was
    /// tripped) - nothing injected, and *not* an error. Distinct from
    /// <see cref="TranscriptionFailed"/>/<see cref="RefinedOffline"/> so a clean
    /// abort isn't masked as a failure; the caller surfaces no notice and returns
    /// quietly to Idle. Appended last: this enum is never serialized, so order is
    /// safe.
    /// </summary>
    Cancelled,
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
    private readonly Action<string>? _onResult;
    private readonly Func<Stream, CancellationToken, Task<string>>? _transcribeFallback;

    public DictationPipeline(
        ITranscriber transcriber,
        ITextInjector injector,
        Func<string, CancellationToken, Task<string>>? refine = null,
        Action<string>? onResult = null,
        Func<Stream, CancellationToken, Task<string>>? transcribeFallback = null)
    {
        _transcriber = transcriber;
        _injector = injector;
        _refine = refine;
        _onResult = onResult;
        _transcribeFallback = transcribeFallback;
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
        var transcribedOffline = false;
        try
        {
            text = await _transcriber.TranscribeAsync(wavAudio, ct);
        }
        catch (OperationCanceledException)
        {
            // Issue 47: a tripped cancellation token is a deliberate abort, not a
            // failure. Cancel means abort, not degrade — return Cancelled without
            // attempting the local fallback, and inject nothing. This clause must
            // precede the catch-all below (C# matches catch clauses top-down) so a
            // clean cancel is never masked as TranscriptionFailed.
            return DictationOutcome.Cancelled;
        }
        catch
        {
            // Fail-soft (design §10): a failed transcription is a notice, not a
            // crash. Issue 30: when an offline fallback is wired (Online source),
            // transcribe locally instead of losing the dictation — mirroring the
            // refinement RefinedOffline fail-soft below. With no fallback (or one
            // that also fails) this stays a TranscriptionFailed notice.
            if (_transcribeFallback is null)
            {
                return DictationOutcome.TranscriptionFailed;
            }

            try
            {
                // The primary attempt may have read the stream to its end; rewind
                // so the local model sees the whole utterance, not zero bytes.
                if (wavAudio.CanSeek)
                {
                    wavAudio.Position = 0;
                }

                text = await _transcribeFallback(wavAudio, ct);
                transcribedOffline = true;
            }
            catch (OperationCanceledException)
            {
                // Issue 47: cancelling the local fallback is a deliberate abort,
                // distinct from the fallback failing. Precedes the catch-all so a
                // clean cancel isn't masked as TranscriptionFailed.
                return DictationOutcome.Cancelled;
            }
            catch
            {
                return DictationOutcome.TranscriptionFailed;
            }
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
            catch (OperationCanceledException)
            {
                // Issue 47: a cancelled refiner is a deliberate abort, not the
                // refiner being unreachable — so this is Cancelled (nothing
                // injected), NOT RefinedOffline (which would inject the raw
                // transcript). Precedes the catch-all so a genuine refiner failure
                // still degrades to RefinedOffline below.
                return DictationOutcome.Cancelled;
            }
            catch
            {
                refinedOffline = true;
            }
        }

        // Issue 48: an Esc after transcription/refinement but before injection is
        // still a deliberate abort. Stop here — inject nothing and record nothing —
        // so a just-finished decode the token never interrupted mid-run doesn't land
        // at the cursor after the user already cancelled.
        if (ct.IsCancellationRequested)
        {
            return DictationOutcome.Cancelled;
        }

        // Report the final text — what is about to go to the cursor — to the
        // optional sink (issue 26: the tray's recent-dictations history). Also on
        // a blocked paste below: the clipboard copy is volatile, the history is
        // the recovery net.
        _onResult?.Invoke(text);

        // A false return means the paste was blocked by the focused app — the
        // injector has left the text on the clipboard, so nothing is lost. This
        // dominates RefinedOffline: the user-visible problem is the text not
        // landing at the cursor, not which version was attempted.
        var injected = await _injector.InjectAsync(text, ct);
        if (!injected)
        {
            return DictationOutcome.InjectionBlocked;
        }

        // Precedence: a local transcription fallback (issue 30) is the more
        // fundamental degradation and implies the network was down — so it
        // dominates RefinedOffline, which is almost always also true offline.
        if (transcribedOffline)
        {
            return DictationOutcome.TranscribedOffline;
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
