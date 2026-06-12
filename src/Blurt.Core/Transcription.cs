namespace Blurt.Core;

/// <summary>
/// Speech → raw text. The seam that lets dictation flow swap between local
/// whisper.cpp and an online API (issue 12), and be faked in tests.
/// </summary>
public interface ITranscriber
{
    /// <summary>
    /// Transcribes a complete utterance and returns the raw text (no
    /// refinement). <paramref name="wavAudio"/> must be a WAV stream of
    /// 16 kHz, 16-bit, mono PCM — the format Whisper expects and the format
    /// <c>AudioRecorder</c> captures, so no resampling happens in between.
    /// </summary>
    Task<string> TranscribeAsync(Stream wavAudio, CancellationToken ct = default);
}

/// <summary>
/// Picks the transcriber for one dictation (issue 12): the configured
/// <see cref="TranscriptionMode"/> chooses local whisper.cpp or the OpenAI
/// Whisper API — except for zero-network dictation (verbatim Pur, design
/// contract), which always stays local. Factories instead of instances so the
/// loser costs nothing: with Online selected the local model is never
/// provisioned, and vice versa no online client is built.
/// </summary>
public static class TranscriberResolver
{
    public static Task<ITranscriber> ResolveAsync(
        TranscriptionMode mode,
        bool zeroNetwork,
        Func<Task<ITranscriber>> local,
        Func<ITranscriber> online)
        => zeroNetwork || mode == TranscriptionMode.Local
            ? local()
            : Task.FromResult(online());
}
