using System.Linq;
using System.Management;

namespace Blurt.App;

/// <summary>
/// The impure WMI shell behind the driver-missing nudge (issue 45). Windows falls
/// back to the <c>"Microsoft Basic Display Adapter"</c> when the real GPU driver is
/// not installed; that is the high-confidence signal ADR-0001 keys the nudge on (the
/// pure decision is Core's <see cref="Blurt.Core.DriverNudge"/>). Best-effort and
/// fail-soft: any WMI failure reports "no signal" so a diagnostic query can never
/// crash the app or false-fire the nudge.
/// </summary>
internal static class DisplayAdapter
{
    private const string BasicDisplayAdapter = "Microsoft Basic Display Adapter";

    /// <summary>
    /// Whether the active display adapter is the Windows basic-display fallback —
    /// i.e. the real GPU driver looks missing. Queries WMI's
    /// <c>Win32_VideoController</c>; the basic adapter only enumerates when it is the
    /// adapter actually in use, so its mere presence is the signal.
    /// </summary>
    public static bool IsBasicDisplayAdapterActive()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name FROM Win32_VideoController");
            using var controllers = searcher.Get();
            return controllers
                .Cast<ManagementBaseObject>()
                .Any(c => string.Equals(
                    c["Name"]?.ToString(), BasicDisplayAdapter, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            // WMI unavailable/blocked — treat as "no signal" so the nudge never fires
            // on a query failure (conservative, per ADR-0001).
            return false;
        }
    }
}
