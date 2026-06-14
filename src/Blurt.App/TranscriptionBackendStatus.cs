using Blurt.Core;
using Whisper.net.LibraryLoader;

namespace Blurt.App;

/// <summary>
/// The active-backend signal (issue 43), queryable by other components — the
/// Settings status line (issue 44) and the driver nudge (issue 45). Whisper.net
/// records which native library it actually loaded in the process-global
/// <see cref="RuntimeOptions.LoadedLibrary"/> once the first <c>WhisperFactory</c>
/// is built (by the startup warmup probe, or the first dictation); this reads it
/// live and maps it through Core's pure <see cref="WhisperBackend.Active"/>.
/// </summary>
internal static class TranscriptionBackendStatus
{
    /// <summary>
    /// The backend local transcription is running on, or <c>null</c> until the
    /// first factory has been built (no native library loaded yet — "pending").
    /// </summary>
    public static TranscriptionBackend? Current =>
        RuntimeOptions.LoadedLibrary is { } loaded ? WhisperBackend.Active(loaded) : null;

    /// <summary>
    /// Whether the Vulkan GPU backend actually loaded. Feeds the driver-missing
    /// nudge (issue 45), which fires only when the GPU did <em>not</em> load.
    /// </summary>
    public static bool VulkanLoaded => RuntimeOptions.LoadedLibrary == RuntimeLibrary.Vulkan;
}
