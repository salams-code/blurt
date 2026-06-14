namespace Blurt.Core;

/// <summary>
/// The deliberately conservative driver-missing nudge decision (ADR-0001, issue 45):
/// suggest installing/repairing the GPU driver only on a high-confidence signal — the
/// active display adapter is the "Microsoft Basic Display Adapter" (Windows' fallback
/// when the real driver is missing) <em>and</em> Vulkan did not load. Every other
/// "no Vulkan" case (a named AMD/NVIDIA/Intel GPU that merely lacks Vulkan) shows only
/// the Settings status line (issue 44), never a popup. The WMI query that detects the
/// basic-adapter signal is the impure shell in the App layer; this is the pure rule.
/// </summary>
public static class DriverNudge
{
    /// <summary>
    /// Whether to show the one-time, dismissible driver-missing nudge: true only when
    /// the basic-display-adapter signal is present (<paramref name="driverMissingSignal"/>),
    /// Vulkan did <em>not</em> load (<paramref name="vulkanLoaded"/> is false), and the
    /// user hasn't already dismissed it (<paramref name="alreadyDismissed"/> is false).
    /// </summary>
    public static bool ShouldShow(bool driverMissingSignal, bool vulkanLoaded, bool alreadyDismissed) =>
        driverMissingSignal && !vulkanLoaded && !alreadyDismissed;
}
