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
