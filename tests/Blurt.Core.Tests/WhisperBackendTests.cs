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

    [Fact]
    public void Active_backend_is_Vulkan_only_when_the_Vulkan_library_loaded()
    {
        // Issue 43: the warmup probe reads Whisper.net's process-global
        // RuntimeOptions.LoadedLibrary after building the factory; this pure
        // mapping turns it into the user-facing backend.
        Assert.Equal(TranscriptionBackend.Vulkan, WhisperBackend.Active(RuntimeLibrary.Vulkan));
    }

    [Theory]
    [InlineData(RuntimeLibrary.Cpu)]
    [InlineData(RuntimeLibrary.CpuNoAvx)]
    [InlineData(RuntimeLibrary.Cuda)]
    [InlineData(RuntimeLibrary.OpenVino)]
    public void Active_backend_is_Cpu_for_any_non_Vulkan_library(RuntimeLibrary loaded)
    {
        // Blurt ships only the Vulkan GPU backend, so anything else that loads —
        // a CPU variant, or a hypothetical other accelerator — is reported as CPU.
        // The "GPU off" vs "no compatible GPU" distinction is the status line's job
        // (issue 44), driven by the preference, not by this mapping.
        Assert.Equal(TranscriptionBackend.Cpu, WhisperBackend.Active(loaded));
    }

    // --- Settings status line (issue 44): effective backend, read-only ---

    [Fact]
    public void Status_text_for_gpu_off_says_cpu_by_choice()
    {
        var text = WhisperBackend.StatusText(GpuPreference.Off, TranscriptionBackend.Cpu);

        Assert.Contains("CPU", text, StringComparison.OrdinalIgnoreCase);
        // The deliberate-off case must read as a choice, not a missing-GPU problem.
        Assert.Contains("off", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Status_text_for_active_vulkan_says_gpu_vulkan()
    {
        var text = WhisperBackend.StatusText(GpuPreference.Auto, TranscriptionBackend.Vulkan);

        Assert.Contains("GPU", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Vulkan", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Status_text_for_cpu_fallback_under_auto_explains_the_missing_gpu()
    {
        var text = WhisperBackend.StatusText(GpuPreference.Auto, TranscriptionBackend.Cpu);

        Assert.Contains("CPU", text, StringComparison.OrdinalIgnoreCase);
        // Distinct from the deliberate-off message: this is an unwanted fallback, so
        // it must NOT read as "off" — it explains there is no usable GPU.
        Assert.DoesNotContain("off", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Status_text_is_pending_until_the_warmup_probe_has_run()
    {
        // null = the factory hasn't been built yet (no model installed, or the probe
        // is still in flight). The line reports "detecting" rather than guessing.
        var text = WhisperBackend.StatusText(GpuPreference.Auto, active: null);

        Assert.Contains("detect", text, StringComparison.OrdinalIgnoreCase);
    }
}
