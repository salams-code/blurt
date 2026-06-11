namespace Blurt.Core;

/// <summary>
/// How dictation picks its capture device: track whatever Windows currently calls
/// the default input (so plugging in a Bluetooth headset just works), or pin a
/// specific device the user chose. Default is <see cref="FollowDefault"/> — it
/// matches the behaviour before issue 16, where capture always opened the default
/// device mapper.
/// </summary>
public enum InputDeviceMode
{
    /// <summary>Record from whatever is the current Windows default input device.</summary>
    FollowDefault,

    /// <summary>Record from a specific device, matched by its product name.</summary>
    Specific,
}

/// <summary>
/// The resolved capture target: either "open the Windows default device mapper"
/// (<see cref="UseDefault"/>, <see cref="DeviceIndex"/> null) or a concrete
/// enumeration index. <see cref="FellBack"/> is true only when the user asked for
/// a <see cref="InputDeviceMode.Specific"/> device that wasn't found among the
/// available ones — the signal the App uses to surface a fail-soft "device gone"
/// notice while still recording from the default.
/// </summary>
public sealed record InputDeviceResolution(bool UseDefault, int? DeviceIndex, bool FellBack);

/// <summary>
/// Pure device-selection decision for the recorder. Given the configured
/// <see cref="InputDeviceMode"/>, the saved product name, and the list of
/// currently-available capture devices (enumerated by the App via NAudio), it
/// decides whether to open the Windows default mapper or a concrete device index,
/// and whether a fallback happened. Enumeration and the actual device open stay in
/// the App; this is the testable decision in the middle.
/// </summary>
public static class InputDeviceResolver
{
    /// <summary>
    /// Resolves the capture target.
    /// <list type="bullet">
    /// <item><see cref="InputDeviceMode.FollowDefault"/> → always use the default mapper.</item>
    /// <item><see cref="InputDeviceMode.Specific"/> with a name present in
    /// <paramref name="available"/> → that device's index (the first match, since
    /// product names aren't guaranteed unique).</item>
    /// <item><see cref="InputDeviceMode.Specific"/> with the saved name absent (or no
    /// saved name) → use the default mapper and flag the fallback, <em>unless</em>
    /// no devices are enumerated at all, in which case there is nothing to fall back
    /// from and the App's own start-failure path reports a missing microphone.</item>
    /// </list>
    /// </summary>
    /// <param name="mode">The configured device mode.</param>
    /// <param name="savedName">
    /// The saved <c>WaveInCapabilities.ProductName</c> to match in
    /// <see cref="InputDeviceMode.Specific"/> mode; ignored in
    /// <see cref="InputDeviceMode.FollowDefault"/>.
    /// </param>
    /// <param name="available">
    /// The currently-available capture devices as (enumeration index, product name)
    /// pairs.
    /// </param>
    public static InputDeviceResolution Resolve(
        InputDeviceMode mode,
        string? savedName,
        IReadOnlyList<(int Index, string Name)> available)
    {
        if (mode == InputDeviceMode.FollowDefault)
        {
            // Whatever Windows calls default right now — no concrete index, no fallback.
            return UseDefault(fellBack: false);
        }

        // Specific mode. With no devices at all there's nothing to match against and
        // nothing to fall back from; let the App's start path report the missing mic
        // rather than raise a spurious "saved device gone" notice.
        if (available.Count == 0)
        {
            return UseDefault(fellBack: false);
        }

        foreach (var (index, name) in available)
        {
            if (name == savedName)
            {
                // Found the saved device (first match wins for duplicate names).
                return new InputDeviceResolution(UseDefault: false, DeviceIndex: index, FellBack: false);
            }
        }

        // Specific device requested but it's gone (unplugged, renamed, or never saved):
        // record from the default and flag it so the App can warn the user.
        return UseDefault(fellBack: true);
    }

    private static InputDeviceResolution UseDefault(bool fellBack) =>
        new(UseDefault: true, DeviceIndex: null, FellBack: fellBack);
}
