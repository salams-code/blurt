using Blurt.Core;
using Whisper.net;

namespace Blurt.App;

/// <summary>
/// Local whisper.cpp transcription via Whisper.net. CPU-bound on the target
/// hardware; the Whisper.net runtime auto-selects a GPU backend only when
/// usable hardware is detected. Language is auto-detected (multilingual model,
/// primary German) rather than pinned, so mixed-language dictation still works.
///
/// The factory memory-maps the multi-hundred-MB model, so it is created once
/// on first use and kept for the lifetime of this instance.
/// </summary>
internal sealed class LocalWhisper : ITranscriber, IDisposable
{
    private readonly string _modelPath;
    private WhisperFactory? _factory;

    /// <param name="modelPath">Path to an existing ggml model file
    /// (ensure via <see cref="ModelProvisioner.EnsureModelAsync"/> first).</param>
    public LocalWhisper(string modelPath)
    {
        _modelPath = modelPath;
    }

    /// <inheritdoc/>
    public async Task<string> TranscribeAsync(Stream wavAudio, CancellationToken ct = default)
    {
        _factory ??= WhisperFactory.FromPath(_modelPath);

        await using var processor = _factory.CreateBuilder()
            .WithLanguage("auto")
            .Build();

        var parts = new List<string>();
        await foreach (var segment in processor.ProcessAsync(wavAudio, ct).ConfigureAwait(false))
        {
            parts.Add(segment.Text);
        }

        return string.Concat(parts).Trim();
    }

    public void Dispose() => _factory?.Dispose();
}
