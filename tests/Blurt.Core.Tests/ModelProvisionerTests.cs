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
    public void Turbo_model_resolves_to_the_quantized_large_v3_turbo_ggml_file_under_the_models_folder()
    {
        // Path resolution must work for any selection, not just the default
        // (issue 18): the higher-quality turbo model resolves to its own file.
        var provisioner = new ModelProvisioner(@"C:\fake\AppData\Roaming", new RecordingDownloader());

        var path = provisioner.ResolvePath(WhisperModel.Turbo);

        Assert.Equal(@"C:\fake\AppData\Roaming\Blurt\models\ggml-large-v3-turbo-q5_0.bin", path);
    }

    [Fact]
    public void ModelsDirectory_is_the_blurt_models_folder_under_app_data()
    {
        // The UI shows this as the target folder for a manual install (issue 18),
        // so it must match exactly where ResolvePath puts the file.
        var provisioner = new ModelProvisioner(@"C:\fake\AppData\Roaming", new RecordingDownloader());

        Assert.Equal(@"C:\fake\AppData\Roaming\Blurt\models", provisioner.ModelsDirectory);
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
    public void FindInstalledModelPath_prefers_the_configured_model_when_it_is_present()
    {
        // Issue 30 offline fallback: when the configured model is on disk, use it.
        var appDataRoot = Directory.CreateTempSubdirectory("blurt-test-").FullName;
        try
        {
            var provisioner = new ModelProvisioner(appDataRoot, new RecordingDownloader());
            Install(provisioner, WhisperModel.Default);
            Install(provisioner, WhisperModel.Turbo);

            var path = provisioner.FindInstalledModelPath(WhisperModel.Turbo);

            Assert.Equal(provisioner.ResolvePath(WhisperModel.Turbo), path);
        }
        finally
        {
            Directory.Delete(appDataRoot, recursive: true);
        }
    }

    [Fact]
    public void FindInstalledModelPath_falls_back_to_any_installed_model_when_the_configured_one_is_absent()
    {
        // The user's real case: Online source, configured WhisperModel is
        // large-v3-turbo (NOT on disk), only small was ever downloaded. Offline,
        // the fallback must use whatever is installed rather than attempt an
        // impossible download of the configured model.
        var appDataRoot = Directory.CreateTempSubdirectory("blurt-test-").FullName;
        try
        {
            var provisioner = new ModelProvisioner(appDataRoot, new RecordingDownloader());
            Install(provisioner, WhisperModel.Default);   // only small is present

            var path = provisioner.FindInstalledModelPath(WhisperModel.Turbo);

            Assert.Equal(provisioner.ResolvePath(WhisperModel.Default), path);
        }
        finally
        {
            Directory.Delete(appDataRoot, recursive: true);
        }
    }

    [Fact]
    public void FindInstalledModelPath_returns_null_when_no_model_is_installed()
    {
        // Nothing on disk → no offline fallback is possible; the caller keeps the
        // dictation fail-soft (TranscriptionFailed) rather than forcing a download.
        var appDataRoot = Directory.CreateTempSubdirectory("blurt-test-").FullName;
        try
        {
            var provisioner = new ModelProvisioner(appDataRoot, new RecordingDownloader());

            Assert.Null(provisioner.FindInstalledModelPath(WhisperModel.Turbo));
        }
        finally
        {
            Directory.Delete(appDataRoot, recursive: true);
        }
    }

    private static void Install(ModelProvisioner provisioner, WhisperModel model)
    {
        var path = provisioner.ResolvePath(model);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, [0x01]);   // stand-in for a downloaded model file
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
