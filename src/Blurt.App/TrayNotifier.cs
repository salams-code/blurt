using Blurt.Core;

namespace Blurt.App;

/// <summary>
/// Renders Core's fail-soft notices as tray balloon tips — the one place
/// <see cref="NotifyIcon.ShowBalloonTip(int, string, string, ToolTipIcon)"/> is
/// called, so every notice in the app shares a single, non-blocking surface.
///
/// Tray-only for now. The overlay channel (issue 06) plugs in here too: this
/// adapter is the seam where a notice fans out to both the tray and the overlay
/// banner, so callers keep going through <see cref="INotifier"/> unchanged.
/// </summary>
internal sealed class TrayNotifier : INotifier
{
    private readonly NotifyIcon _trayIcon;

    public TrayNotifier(NotifyIcon trayIcon) => _trayIcon = trayIcon;

    public void Notify(string message, NoticeLevel level)
    {
        // Error notices linger longer than passing info/warnings so the user has
        // time to read what failed.
        var timeoutMs = level == NoticeLevel.Error ? 5000 : 3000;
        _trayIcon.ShowBalloonTip(timeoutMs, AppInfo.Name, message, ToIcon(level));

        // Issue 06 hook: also drive the overlay banner from here once it exists,
        // so a single Notify() reaches both channels.
    }

    private static ToolTipIcon ToIcon(NoticeLevel level) => level switch
    {
        NoticeLevel.Info => ToolTipIcon.Info,
        NoticeLevel.Warning => ToolTipIcon.Warning,
        NoticeLevel.Error => ToolTipIcon.Error,
        _ => ToolTipIcon.None,
    };
}
