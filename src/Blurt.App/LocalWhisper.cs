using Blurt.Core;
using Whisper.net;

namespace Blurt.App;

/// <summary>
/// Local whisper.cpp transcription via Whisper.net. The active backend (Vulkan GPU
/// vs CPU) is chosen by Whisper.net's loader from the global
/// <c>RuntimeOptions.RuntimeLibraryOrder</c> set at startup (issue 42); on the
/// target hardware Vulkan is ~3x faster (ADR-0001). Language is auto-detected
/// (multilingual model, primary German) rather than pinned, so mixed-language
/// dictation still works.
///
/// The factory memory-maps the multi-hundred-MB model and (on Vulkan) compiles
/// shaders, so it is built exactly once and shared: the startup warmup probe
/// (issue 43) builds it in the background via <see cref="EnsureFactoryAsync"/>, and
/// a dictation fired while that build is still in flight awaits the same instance
/// instead of starting a second native load. The build is thread-safe because the
/// warmup runs on a background thread while dictation resolves on the UI thread.
/// </summary>
internal sealed class LocalWhisper : ITranscriber, IDisposable
{
    private readonly string _modelPath;

    // The single shared factory build. Guarded by _gate because the warmup probe
    // (background thread) and the first dictation (UI thread) can race to start it;
    // a faulted attempt is forgotten so a transient build failure is retried rather
    // than poisoning every later dictation. _built holds the result purely so Dispose
    // can release it without forcing a build it never needed.
    private readonly object _gate = new();
    private Task<WhisperFactory>? _factory;
    private WhisperFactory? _built;

    /// <param name="modelPath">Path to an existing ggml model file
    /// (ensure via <see cref="ModelProvisioner.EnsureModelAsync"/> first).</param>
    public LocalWhisper(string modelPath)
    {
        _modelPath = modelPath;
    }

    /// <summary>
    /// Builds (or returns) the shared <see cref="WhisperFactory"/>. The startup warmup
    /// probe (issue 43) calls this so the first dictation reuses the same instance — no
    /// double model load — and the one-time Vulkan shader-compile cost is paid in the
    /// background. The build runs off the calling thread (it P/Invokes whisper.cpp).
    /// </summary>
    public Task<WhisperFactory> EnsureFactoryAsync()
    {
        lock (_gate)
        {
            if (_factory is null || _factory.IsFaulted || _factory.IsCanceled)
            {
                _factory = Task.Run(() => _built = WhisperFactory.FromPath(_modelPath));
            }

            return _factory;
        }
    }

    /// <inheritdoc/>
    public async Task<string> TranscribeAsync(Stream wavAudio, CancellationToken ct = default)
    {
        var factory = await EnsureFactoryAsync().ConfigureAwait(false);

        await using var processor = factory.CreateBuilder()
            .WithLanguage("auto")
            .Build();

        var parts = new List<string>();
        await foreach (var segment in processor.ProcessAsync(wavAudio, ct).ConfigureAwait(false))
        {
            parts.Add(segment.Text);
        }

        return string.Concat(parts).Trim();
    }

    public void Dispose() => _built?.Dispose();
}
