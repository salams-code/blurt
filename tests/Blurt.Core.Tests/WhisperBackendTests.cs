using Blurt.Core;
using Whisper.net.LibraryLoader;
using Xunit;

namespace Blurt.Core.Tests;

public class WhisperBackendTests
{
    [Fact]
    public void Auto_prefers_Vulkan_then_falls_back_to_Cpu()
    {
        // ADR-0001: Auto means "prefer the GPU, fall back to CPU automatically".
        // The order is handed to Whisper.net's loader, which tries each in turn.
        Assert.Equal(
            new[] { RuntimeLibrary.Vulkan, RuntimeLibrary.Cpu },
            WhisperBackend.OrderFor(GpuPreference.Auto));
    }

    [Fact]
    public void Off_is_Cpu_only()
    {
        // Off forces CPU — the GPU backend is never even attempted.
        Assert.Equal(
            new[] { RuntimeLibrary.Cpu },
            WhisperBackend.OrderFor(GpuPreference.Off));
    }

    [Fact]
    public void Auto_is_the_zero_value_so_pre_existing_configs_default_to_gpu_on()
    {
        // ADR-0001/issue 42: Auto must be the first enum value (0). A config.json
        // written before this setting existed has no field for it and deserialises
        // to the zero value — which must be GPU-on (Auto), not Off, after upgrade.
        Assert.Equal(GpuPreference.Auto, default(GpuPreference));
        Assert.Equal(GpuPreference.Auto, BlurtConfig.Default.GpuPreference);
    }
}
