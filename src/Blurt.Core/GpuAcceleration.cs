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
/// The backend local transcription actually runs on (ADR-0001, issue 43), derived
/// from the library Whisper.net's loader could load. Reported in the Settings status
/// line (issue 44) and feeds the driver nudge (issue 45).
/// </summary>
public enum TranscriptionBackend
{
    /// <summary>GPU acceleration via the Vulkan whisper.cpp backend.</summary>
    Vulkan,

    /// <summary>CPU — either by preference (Off), or because no usable GPU/driver was found.</summary>
    Cpu,
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

    /// <summary>
    /// The effective backend given the library Whisper.net actually loaded
    /// (<c>RuntimeOptions.LoadedLibrary</c>): <see cref="TranscriptionBackend.Vulkan"/>
    /// only when Vulkan loaded, otherwise <see cref="TranscriptionBackend.Cpu"/>.
    /// Blurt ships only the Vulkan GPU backend, so every other loaded library maps to
    /// CPU for the user-facing status (issue 43).
    /// </summary>
    public static TranscriptionBackend Active(RuntimeLibrary loaded) =>
        loaded == RuntimeLibrary.Vulkan ? TranscriptionBackend.Vulkan : TranscriptionBackend.Cpu;

    /// <summary>
    /// The read-only Settings status line (issue 44) reporting the <em>effective</em>
    /// backend: the user's <paramref name="preference"/> plus what the probe found
    /// (<paramref name="active"/>; <c>null</c> until the factory has been built). It
    /// distinguishes the three cases the user cares about — GPU active, an unwanted
    /// CPU fallback (no usable GPU), and CPU by choice — plus a "detecting" state
    /// while the warmup probe is still in flight.
    /// </summary>
    public static string StatusText(GpuPreference preference, TranscriptionBackend? active)
    {
        // Off is a deliberate choice — say so, and never as a missing-GPU problem.
        // (We know the effective backend is CPU without waiting on the probe.)
        if (preference == GpuPreference.Off)
        {
            return "Active backend: CPU (GPU acceleration off)";
        }

        return active switch
        {
            TranscriptionBackend.Vulkan => "Active backend: GPU (Vulkan)",
            TranscriptionBackend.Cpu => "Active backend: CPU — no compatible GPU/driver found",
            // null: the warmup probe hasn't built a factory yet (no model installed,
            // or the build is still running). Report it rather than guessing.
            _ => "Active backend: detecting…",
        };
    }
}
