using Blurt.Core;
using Xunit;

namespace Blurt.Core.Tests;

public class ModelProvisionerTests
{
    /// <summary>Test double for the download seam; never touches the network.</summary>
    private sealed class RecordingDownloader : IModelDownloader
    {
        public List<(WhisperModel Model, string TargetPath)> Calls { get; } = [];

        public Task DownloadAsync(WhisperModel model, string targetPath, CancellationToken ct = default)
        {
            Calls.Add((model, targetPath));
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void Default_model_resolves_to_the_quantized_small_ggml_file_under_the_models_folder()
    {
        var provisioner = new ModelProvisioner(@"C:\fake\AppData\Roaming", new RecordingDownloader());

        var path = provisioner.ResolvePath(WhisperModel.Default);

        Assert.Equal(@"C:\fake\AppData\Roaming\Blurt\models\ggml-small-q5_1.bin", path);
    }

    [Fact]
    public async Task EnsureModel_downloads_the_model_to_its_resolved_path_when_missing()
    {
        // A temp root that exists, but contains no Blurt\models\... file yet.
        var appDataRoot = Directory.CreateTempSubdirectory("blurt-test-").FullName;
        try
        {
            var downloader = new RecordingDownloader();
            var provisioner = new ModelProvisioner(appDataRoot, downloader);

            var path = await provisioner.EnsureModelAsync(WhisperModel.Default);

            var call = Assert.Single(downloader.Calls);
            Assert.Equal(WhisperModel.Default, call.Model);
            Assert.Equal(provisioner.ResolvePath(WhisperModel.Default), call.TargetPath);
            Assert.Equal(call.TargetPath, path);
            // The models directory must exist by the time the downloader runs,
            // so implementations can write straight to the target path.
            Assert.True(Directory.Exists(Path.GetDirectoryName(call.TargetPath)));
        }
        finally
        {
            Directory.Delete(appDataRoot, recursive: true);
        }
    }

    [Fact]
    public async Task EnsureModel_does_not_download_when_the_model_file_already_exists()
    {
        var appDataRoot = Directory.CreateTempSubdirectory("blurt-test-").FullName;
        try
        {
            var downloader = new RecordingDownloader();
            var provisioner = new ModelProvisioner(appDataRoot, downloader);
            var existing = provisioner.ResolvePath(WhisperModel.Default);
            Directory.CreateDirectory(Path.GetDirectoryName(existing)!);
            File.WriteAllBytes(existing, [0x01]);   // stand-in for a previously downloaded model

            var path = await provisioner.EnsureModelAsync(WhisperModel.Default);

            Assert.Empty(downloader.Calls);
            Assert.Equal(existing, path);
        }
        finally
        {
            Directory.Delete(appDataRoot, recursive: true);
        }
    }

    [Fact]
    public void IsModelPresent_reflects_whether_the_model_file_exists()
    {
        // The UI needs this before EnsureModel so it can announce a first-run
        // download (hundreds of MB) instead of silently appearing to hang.
        var appDataRoot = Directory.CreateTempSubdirectory("blurt-test-").FullName;
        try
        {
            var provisioner = new ModelProvisioner(appDataRoot, new RecordingDownloader());

            Assert.False(provisioner.IsModelPresent(WhisperModel.Default));

            var path = provisioner.ResolvePath(WhisperModel.Default);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, [0x01]);

            Assert.True(provisioner.IsModelPresent(WhisperModel.Default));
        }
        finally
        {
            Directory.Delete(appDataRoot, recursive: true);
        }
    }
}
