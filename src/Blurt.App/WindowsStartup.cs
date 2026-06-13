using Blurt.Core;
using Microsoft.Win32;

namespace Blurt.App;

/// <summary>
/// "Start Blurt when Windows starts", via the per-user Run key
/// (<c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c>) — no admin rights
/// needed, unlike the machine-wide key or a Task Scheduler entry. The key's
/// presence is the single source of truth: the Settings checkbox reflects it on
/// load and writes/removes it on save, so it stays correct even if the user edits
/// startup apps elsewhere.
///
/// The registered command is the current executable's absolute path. For a
/// portable build that means moving the folder breaks the entry until the user
/// re-toggles it — surfaced in the Settings hint.
/// </summary>
internal static class WindowsStartup
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = AppInfo.Name;

    /// <summary>True when a Run entry for Blurt currently exists.</summary>
    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(ValueName) is string value && value.Length > 0;
    }

    /// <summary>Adds (quoted current exe path) or removes the Run entry to match
    /// <paramref name="enabled"/>. Idempotent; a no-op if already in that state.</summary>
    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (enabled)
        {
            // Environment.ProcessPath is the running Blurt.exe — correct for a
            // portable install wherever it sits. Quote it against spaces in the path.
            key.SetValue(ValueName, $"\"{Environment.ProcessPath}\"");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
