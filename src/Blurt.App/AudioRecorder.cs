using Blurt.Core;
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

    // NAudio's device number for "the current Windows default input device" — the
    // WaveIn device mapper. Re-opened on every Start, so whatever is default at
    // dictation time is what records (plugging in a Bluetooth headset takes effect
    // on the next press with no reconfiguring).
    private const int DefaultDeviceNumber = -1;

    public bool IsRecording => _waveIn is not null;

    /// <summary>
    /// Resolves the capture device from the configured <paramref name="mode"/> and
    /// saved <paramref name="savedDeviceName"/> against the devices currently
    /// enumerated by NAudio, opens it, and starts recording. Returns the resolution
    /// so the caller can surface a fail-soft notice when the saved device is gone
    /// (<see cref="InputDeviceResolution.FellBack"/>) — capture still proceeds from
    /// the Windows default in that case. Idempotent while already recording.
    /// </summary>
    public InputDeviceResolution Start(InputDeviceMode mode, string? savedDeviceName)
    {
        var resolution = InputDeviceResolver.Resolve(mode, savedDeviceName, EnumerateDevices());

        if (_waveIn is not null)
        {
            return resolution;   // already recording (e.g. auto-repeat of the held key)
        }

        var deviceNumber = resolution.DeviceIndex ?? DefaultDeviceNumber;

        // Build everything into locals and commit to the fields only once recording
        // has actually started. If any step throws (device gone, permission denied,
        // a WaveFileWriter hiccup), the fields stay null — so IsRecording can never
        // report true over a half-built recorder, which is what later made Stop()
        // dereference a null writer/buffer and crash the app from the hook callback.
        var buffer = new MemoryStream();
        WaveInEvent? waveIn = null;
        WaveFileWriter? writer = null;
        try
        {
            waveIn = new WaveInEvent
            {
                DeviceNumber = deviceNumber,
                WaveFormat = new WaveFormat(rate: 16000, bits: 16, channels: 1),
            };
            // IgnoreDisposeStream: disposing the writer finalizes the WAV header
            // without closing the MemoryStream we still need to hand out.
            writer = new WaveFileWriter(new IgnoreDisposeStream(buffer), waveIn.WaveFormat);

            // Publish the writer/buffer before OnDataAvailable can fire, then start.
            _buffer = buffer;
            _writer = writer;
            waveIn.DataAvailable += OnDataAvailable;
            _waveIn = waveIn;
            waveIn.StartRecording();
        }
        catch
        {
            // No capture device / permission denied (or any construction failure):
            // tear down whatever was built and leave a clean not-recording state, then
            // rethrow for the caller to surface as a fail-soft notice rather than a crash.
            if (waveIn is not null)
            {
                waveIn.DataAvailable -= OnDataAvailable;
                waveIn.Dispose();
            }
            writer?.Dispose();
            buffer.Dispose();
            _waveIn = null;
            _writer = null;
            _buffer = null;
            throw;
        }

        return resolution;
    }

    // Enumerate the current capture devices as (index, ProductName) pairs for the
    // Core resolver. ProductName is the only stable handle NAudio exposes; the
    // index is what WaveInEvent.DeviceNumber wants for a specific device.
    private static IReadOnlyList<(int Index, string Name)> EnumerateDevices()
    {
        var devices = new List<(int, string)>();
        for (var i = 0; i < WaveInEvent.DeviceCount; i++)
            devices.Add((i, WaveInEvent.GetCapabilities(i).ProductName));
        return devices;
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

        // Detach into locals before disposing, and null-check rather than assume the
        // writer/buffer are set: with an atomic Start they always are, but Stop must
        // never throw an NRE here — it runs inside the keyboard-hook callback, where
        // an escaping exception kills the whole process.
        var writer = _writer;
        var buffer = _buffer;
        _waveIn = null;
        _writer = null;
        _buffer = null;

        writer?.Dispose();   // flushes data length into the WAV header

        if (buffer is null)
        {
            return new MemoryStream();   // nothing captured — hand back an empty stream
        }

        buffer.Position = 0;
        return buffer;
    }

    /// <summary>
    /// Non-blocking discard for a take that was never meant as speech — the
    /// flex-slot tap path (issue 21). Detaches all state immediately (so
    /// <see cref="IsRecording"/> is false and the next press starts fresh) and
    /// tears the device down on the thread pool: the UI thread never waits for
    /// NAudio's device thread to drain a recording we're throwing away.
    /// No-op when not recording.
    /// </summary>
    public void Discard()
    {
        var waveIn = _waveIn;
        if (waveIn is null)
        {
            return;
        }

        // Unsubscribe FIRST: a late device buffer must never land in _writer —
        // by the time it fires, that field could already belong to the *next*
        // recording of a rapid tap→hold sequence.
        waveIn.DataAvailable -= OnDataAvailable;

        var writer = _writer;
        var buffer = _buffer;
        _waveIn = null;
        _writer = null;
        _buffer = null;

        _ = Task.Run(() =>
        {
            try
            {
                waveIn.StopRecording();
                waveIn.Dispose();
            }
            catch
            {
                // Discarding: there is nothing to save, so teardown hiccups
                // (device already gone, etc.) are not worth surfacing.
            }
            finally
            {
                writer?.Dispose();
                buffer?.Dispose();
            }
        });
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
