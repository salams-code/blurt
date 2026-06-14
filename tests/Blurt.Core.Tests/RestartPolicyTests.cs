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
    public void Changing_a_live_setting_needs_no_restart()
    {
        // Sound, hotkeys, prompts, overlay anchor etc. all apply live — only the GPU
        // preference needs a relaunch, so changing an unrelated field must not prompt.
        var running = BlurtConfig.Default;
        var saved = running with { SoundEnabled = !running.SoundEnabled };

        Assert.False(RestartPolicy.RequiresRestart(running, saved));
    }
}
