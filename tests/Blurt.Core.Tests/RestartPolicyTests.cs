using Blurt.Core;
using Xunit;

namespace Blurt.Core.Tests;

public class RestartPolicyTests
{
    [Fact]
    public void A_gpu_preference_change_requires_a_restart()
    {
        // GpuPreference maps to Whisper.net's global-static native load order, set
        // once before the first WhisperFactory — it cannot change in-process, so a
        // change only applies after a relaunch.
        var running = BlurtConfig.Default with { GpuPreference = GpuPreference.Auto };
        var saved = running with { GpuPreference = GpuPreference.Off };

        Assert.True(RestartPolicy.RequiresRestart(running, saved));
    }

    [Fact]
    public void The_same_gpu_preference_needs_no_restart()
    {
        // Toggling back to the running value (or never touching it) is already live —
        // no relaunch needed.
        var running = BlurtConfig.Default with { GpuPreference = GpuPreference.Off };

        Assert.False(RestartPolicy.RequiresRestart(running, running));
    }

    [Fact]
    public void A_local_model_change_requires_a_restart()
    {
        // The selected ggml model is loaded once (warmup probe / first dictation) and
        // cached for the process, so switching it also only applies after a relaunch.
        var running = BlurtConfig.Default with { WhisperModel = WhisperModel.Default };
        var saved = running with { WhisperModel = WhisperModel.Turbo };

        Assert.True(RestartPolicy.RequiresRestart(running, saved));
    }

    [Fact]
    public void Changing_a_live_setting_needs_no_restart()
    {
        // Sound, hotkeys, prompts, overlay anchor, transcription source etc. all apply
        // live — only GPU preference and the local model need a relaunch, so changing
        // an unrelated field must not prompt.
        var running = BlurtConfig.Default;
        var saved = running with { SoundEnabled = !running.SoundEnabled };

        Assert.False(RestartPolicy.RequiresRestart(running, saved));
    }

    [Fact]
    public void Restart_required_changes_names_each_changed_setting()
    {
        // The dialog tells the user WHAT needs the restart, so the decision returns the
        // human-facing names of the settings that changed.
        var running = BlurtConfig.Default with
        {
            GpuPreference = GpuPreference.Auto,
            WhisperModel = WhisperModel.Default,
        };
        var saved = running with
        {
            GpuPreference = GpuPreference.Off,
            WhisperModel = WhisperModel.Turbo,
        };

        var changes = RestartPolicy.RestartRequiredChanges(running, saved);

        Assert.Contains("GPU acceleration", changes);
        Assert.Contains("local model", changes);
    }

    [Fact]
    public void No_restart_required_changes_when_nothing_relevant_changed()
    {
        var running = BlurtConfig.Default;

        Assert.Empty(RestartPolicy.RestartRequiredChanges(running, running));
    }
}
