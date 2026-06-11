using NAudio.Utils;
using NAudio.Wave;

namespace Blurt.App;

/// <summary>
/// Thin NAudio adapter for push-to-talk capture: <see cref="Start"/> on trigger
/// down, <see cref="Stop"/> on trigger up. Records straight to 16 kHz, 16-bit,
/// mono PCM — the format Whisper expects (<see cref="Blurt.Core.ITranscriber"/>
/// contract) — so no resampling step is needed. Dictations are short, so the
/// WAV is buffered in memory rather than spooled to disk.
///
/// Hardware-bound; verified manually on Windows like the keyboard hook.
/// </summary>
internal sealed class AudioRecorder : IDisposable
{
    private WaveInEvent? _waveIn;
    private MemoryStream? _buffer;
    private WaveFileWriter? _writer;

    public bool IsRecording => _waveIn is not null;

    /// <summary>Opens the default capture device and starts recording.</summary>
    public void Start()
    {
        if (_waveIn is not null)
        {
            return;   // already recording (e.g. auto-repeat of the held key)
        }

        _buffer = new MemoryStream();
        _waveIn = new WaveInEvent { WaveFormat = new WaveFormat(rate: 16000, bits: 16, channels: 1) };
        // IgnoreDisposeStream: disposing the writer finalizes the WAV header
        // without closing the MemoryStream we still need to hand out.
        _writer = new WaveFileWriter(new IgnoreDisposeStream(_buffer), _waveIn.WaveFormat);
        _waveIn.DataAvailable += OnDataAvailable;

        try
        {
            _waveIn.StartRecording();
        }
        catch
        {
            // No capture device / permission denied: NAudio throws here. Reset to
            // a clean not-recording state (so IsRecording is false and the next
            // press starts fresh) and rethrow for the caller to surface as a
            // fail-soft notice rather than a crash.
            _waveIn.DataAvailable -= OnDataAvailable;
            _waveIn.Dispose();
            _writer.Dispose();
            _waveIn = null;
            _writer = null;
            _buffer = null;
            throw;
        }
    }

    /// <summary>
    /// Stops recording and returns the captured audio as a complete WAV stream
    /// positioned at the start, ready for <see cref="Blurt.Core.ITranscriber"/>.
    /// The caller owns the returned stream.
    /// </summary>
    public Stream Stop()
    {
        var waveIn = _waveIn ?? throw new InvalidOperationException("Not recording.");

        // Wait for the device thread to drain its last buffer before the WAV
        // header is finalized; otherwise the utterance tail gets clipped.
        // (_writer must stay assigned until then — OnDataAvailable uses it.)
        using (var stopped = new ManualResetEventSlim(false))
        {
            waveIn.RecordingStopped += (_, _) => stopped.Set();
            waveIn.StopRecording();
            stopped.Wait(TimeSpan.FromSeconds(2));
        }

        waveIn.DataAvailable -= OnDataAvailable;
        waveIn.Dispose();
        _writer!.Dispose();   // flushes data length into the WAV header

        var buffer = _buffer!;
        _waveIn = null;
        _writer = null;
        _buffer = null;

        buffer.Position = 0;
        return buffer;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e) =>
        _writer?.Write(e.Buffer, 0, e.BytesRecorded);

    public void Dispose()
    {
        if (_waveIn is not null)
        {
            Stop().Dispose();   // abandon any in-flight recording
        }
    }
}
