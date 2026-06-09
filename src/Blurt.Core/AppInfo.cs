namespace Blurt.Core;

/// <summary>
/// Static facts about the application. Lives in the core library so both the
/// tray host and the test project share one source of truth.
/// </summary>
public static class AppInfo
{
    /// <summary>Product name shown in the tray tooltip and window titles.</summary>
    public const string Name = "Blurt";
}
