using Blurt.Core;
using Whisper.net.Ggml;

namespace Blurt.App;

/// <summary>
/// Real implementation of the core's download seam: fetches the ggml model
/// from Hugging Face via Whisper.net's downloader. Only ever invoked at app
/// runtime by <see cref="ModelProvisioner"/> when the model file is missing —
/// never during build or unit tests.
/// </summary>
internal sealed class GgmlModelDownloader : IModelDownloader
{
    public async Task DownloadAsync(WhisperModel model, string targetPath, CancellationToken ct = default)
    {
        var type = Parse<GgmlType>(model.Size);
        var quantization = Parse<QuantizationType>(model.Quantization);

        await using var source = await WhisperGgmlDownloader.Default
            .GetGgmlModelAsync(type, quantization, ct)
            .ConfigureAwait(false);

        // Download to a sibling temp file, then move: a half-finished download
        // must never be mistaken for a present model by IsModelPresent.
        var tempPath = targetPath + ".download";
        await using (var file = File.Create(tempPath))
        {
            await source.CopyToAsync(file, ct).ConfigureAwait(false);
        }

        File.Move(tempPath, targetPath, overwrite: true);
    }

    // The core names variants the way whisper.cpp files do ("small", "q5_1");
    // Whisper.net uses enums (Small, Q5_1). Case-insensitive parse bridges them.
    private static T Parse<T>(string name) where T : struct, Enum =>
        Enum.TryParse<T>(name, ignoreCase: true, out var value)
            ? value
            : throw new NotSupportedException($"Unknown Whisper model variant '{name}' for {typeof(T).Name}.");
}
