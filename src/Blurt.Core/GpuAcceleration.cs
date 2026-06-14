using Whisper.net.LibraryLoader;

namespace Blurt.Core;

/// <summary>
/// User preference for GPU-accelerated local transcription (ADR-0001).
/// <see cref="Auto"/> prefers the Vulkan whisper.cpp backend and falls back to CPU
/// automatically; <see cref="Off"/> forces CPU. Declared first → zero value → a
/// config written before this setting existed deserialises to <see cref="Auto"/>
/// (GPU-on after upgrade), per the ADR.
/// </summary>
public enum GpuPreference
{
    /// <summary>Prefer the GPU (Vulkan), fall back to CPU when unavailable. The default.</summary>
    Auto,

    /// <summary>Force CPU transcription — never attempt the GPU backend.</summary>
    Off,
}

/// <summary>
/// Pure mapping from the user's <see cref="GpuPreference"/> to Whisper.net's native
/// runtime-library load order (ADR-0001, issue 42). The order is global-static in
/// Whisper.net and must be set once before the first <c>WhisperFactory</c>; keeping
/// the decision here — pure and unit-tested — separates it from that impure startup
/// wiring in the App shell.
/// </summary>
public static class WhisperBackend
{
    /// <summary>
    /// The runtime load order for <paramref name="preference"/>: <c>Auto → [Vulkan, Cpu]</c>
    /// (prefer the GPU, let Whisper.net's loader fall back to CPU when Vulkan is
    /// unavailable), <c>Off → [Cpu]</c> (CPU only, GPU never attempted).
    /// </summary>
    public static IReadOnlyList<RuntimeLibrary> OrderFor(GpuPreference preference) => preference switch
    {
        GpuPreference.Auto => [RuntimeLibrary.Vulkan, RuntimeLibrary.Cpu],
        GpuPreference.Off => [RuntimeLibrary.Cpu],
        // An unknown value (e.g. a future enum member) is treated like Off — CPU is
        // always available, so the safe default is the one that always transcribes.
        _ => [RuntimeLibrary.Cpu],
    };
}
