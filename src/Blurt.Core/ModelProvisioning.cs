namespace Blurt.Core;

/// <summary>
/// A Whisper ggml model variant, identified the way whisper.cpp names its
/// model files: <c>ggml-{size}-{quantization}.bin</c>. Multilingual on purpose
/// (no <c>.en</c> suffix) — Blurt's primary language is German and the English
/// mode relies on Whisper's translate capability.
/// </summary>
public sealed record WhisperModel(string Size, string Quantization)
{
    /// <summary>
    /// Default per the design contract: <c>small</c> quantized to q5_1
    /// (~460 MB, ~2–4 s per dictation on CPU).
    /// </summary>
    public static WhisperModel Default { get; } = new("small", "q5_1");

    /// <summary>
    /// The higher-quality local option the settings window offers alongside
    /// <see cref="Default"/>: <c>large-v3-turbo</c> quantized to q5_0 (~574 MB,
    /// markedly more accurate, still turbo-fast). Lets the user trade size/latency
    /// for accuracy. The <c>q5_0</c> suffix is the exact quantization published in
    /// the ggerganov/whisper.cpp repo for this model (turbo has no q5_1 variant).
    /// </summary>
    public static WhisperModel Turbo { get; } = new("large-v3-turbo", "q5_0");

    /// <summary>File name as published in the ggerganov/whisper.cpp model repo.</summary>
    public string FileName => $"ggml-{Size}-{Quantization}.bin";

    /// <summary>
    /// Canonical Hugging Face <c>resolve</c> URL the model file can be downloaded
    /// from. Because the corporate proxy blocks the in-app download, the UI shows
    /// this link so the matching file can be installed by hand — derived from the
    /// selection, so it always points at the file the app expects.
    /// </summary>
    public string DownloadUrl =>
        $"https://huggingface.co/ggerganov/whisper.cpp/resolve/main/{FileName}";
}

/// <summary>
/// Seam for fetching a model file. The real implementation (Whisper.net's ggml
/// downloader) lives in the app layer; the core only decides <em>whether</em>
/// a download is needed, so that decision stays unit-testable offline.
/// </summary>
public interface IModelDownloader
{
    /// <summary>Downloads <paramref name="model"/> to <paramref name="targetPath"/>.</summary>
    Task DownloadAsync(WhisperModel model, string targetPath, CancellationToken ct = default);
}

/// <summary>
/// Decides where Whisper models live on disk and when one must be downloaded.
/// Models go under <c>&lt;appDataRoot&gt;\Blurt\models\</c> (design contract:
/// keep the distributable small; fetch the model on first run).
/// </summary>
public sealed class ModelProvisioner
{
    private readonly string _modelsDirectory;
    private readonly IModelDownloader _downloader;

    /// <param name="appDataRoot">
    /// The roaming app-data root (e.g. <c>%APPDATA%</c>); injected rather than
    /// read from the environment so tests can point it anywhere.
    /// </param>
    public ModelProvisioner(string appDataRoot, IModelDownloader downloader)
    {
        _modelsDirectory = Path.Combine(appDataRoot, AppInfo.Name, "models");
        _downloader = downloader;
    }

    /// <summary>
    /// The folder all models live in (<c>&lt;appDataRoot&gt;\Blurt\models</c>).
    /// Surfaced so the UI can show it as the target for a manual install (issue 18)
    /// without re-deriving the path and risking a mismatch with <see cref="ResolvePath"/>.
    /// </summary>
    public string ModelsDirectory => _modelsDirectory;

    /// <summary>Absolute path the given model lives at (whether or not it exists yet).</summary>
    public string ResolvePath(WhisperModel model) => Path.Combine(_modelsDirectory, model.FileName);

    /// <summary>
    /// Whether the model file is already on disk. Lets the UI announce a
    /// first-run download (hundreds of MB) before <see cref="EnsureModelAsync"/>
    /// blocks on it, instead of silently appearing to hang.
    /// </summary>
    public bool IsModelPresent(WhisperModel model) => File.Exists(ResolvePath(model));

    /// <summary>
    /// Makes sure the model file is present, downloading it only on first run
    /// (i.e. when the file is missing), and returns its path. The models
    /// directory is created up front so the downloader can write straight to
    /// the target path.
    /// </summary>
    public async Task<string> EnsureModelAsync(WhisperModel model, CancellationToken ct = default)
    {
        var path = ResolvePath(model);
        if (!IsModelPresent(model))
        {
            Directory.CreateDirectory(_modelsDirectory);
            await _downloader.DownloadAsync(model, path, ct).ConfigureAwait(false);
        }

        return path;
    }
}
