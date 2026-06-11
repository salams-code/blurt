using Blurt.Core;
using Xunit;

namespace Blurt.Core.Tests;

public class WhisperModelTests
{
    [Fact]
    public void Default_is_the_quantized_small_model()
    {
        // The design default stays small/q5_1 — the per-selection download
        // guidance (issue 18) is derived from this, so the filename is load-bearing.
        Assert.Equal("small", WhisperModel.Default.Size);
        Assert.Equal("q5_1", WhisperModel.Default.Quantization);
        Assert.Equal("ggml-small-q5_1.bin", WhisperModel.Default.FileName);
    }

    [Fact]
    public void Turbo_is_the_published_large_v3_turbo_q5_0_model()
    {
        // The higher-quality option (issue 18). Filename and quantization match
        // the file actually published in ggerganov/whisper.cpp — a manual install
        // only matches what the app expects if these are exactly right.
        Assert.Equal("large-v3-turbo", WhisperModel.Turbo.Size);
        Assert.Equal("q5_0", WhisperModel.Turbo.Quantization);
        Assert.Equal("ggml-large-v3-turbo-q5_0.bin", WhisperModel.Turbo.FileName);
    }

    [Fact]
    public void DownloadUrl_is_the_canonical_huggingface_resolve_url_for_the_default_model()
    {
        Assert.Equal(
            "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small-q5_1.bin",
            WhisperModel.Default.DownloadUrl);
    }

    [Fact]
    public void DownloadUrl_is_the_canonical_huggingface_resolve_url_for_the_turbo_model()
    {
        Assert.Equal(
            "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v3-turbo-q5_0.bin",
            WhisperModel.Turbo.DownloadUrl);
    }
}
