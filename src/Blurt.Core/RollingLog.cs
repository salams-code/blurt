namespace Blurt.Core;

/// <summary>
/// A tiny append-only text log that caps its own size, so a long-running tray app
/// never grows an unbounded file. When the active file reaches
/// <see cref="_maxBytes"/> it is rotated to a single <c>.1</c> backup (the previous
/// backup is dropped), so at most ~2× <see cref="_maxBytes"/> ever sits on disk.
///
/// Built for crash capture: <see cref="Write"/> is called from global
/// unhandled-exception handlers that may fire on any thread, so it locks and —
/// crucially — <b>never throws</b>. A logging failure must not become a second
/// crash on top of the one being recorded; any IO error is swallowed.
/// </summary>
public sealed class RollingLog
{
    private readonly string _path;
    private readonly long _maxBytes;
    private readonly object _gate = new();

    /// <param name="path">Full path of the active log file; its directory is
    /// created on demand.</param>
    /// <param name="maxBytes">Rotate once the active file reaches this size
    /// (default 512 KB).</param>
    public RollingLog(string path, long maxBytes = 512 * 1024)
    {
        _path = path;
        _maxBytes = maxBytes;
    }

    /// <summary>The active log file's path, so callers can point a user at it.</summary>
    public string Path => _path;

    /// <summary>The <c>.1</c> backup's path (may not exist until the first rotation).</summary>
    public string BackupPath => _path + ".1";

    /// <summary>
    /// Append one timestamped line. Rotates first when the active file has reached
    /// the size cap. Never throws — IO errors (locked file, missing drive) are
    /// swallowed, because logging runs inside crash handlers where a throw would
    /// mask the original failure.
    /// </summary>
    public void Write(string message)
    {
        lock (_gate)
        {
            try
            {
                var directory = System.IO.Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                RotateIfFull();
                File.AppendAllText(
                    _path,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}");
            }
            catch
            {
                // Logging must never throw — a failed write is preferable to a
                // second crash while recording the first.
            }
        }
    }

    // Move the full active file to the single .1 backup (replacing any previous
    // backup) so the next Write starts a fresh file. Best-effort: if the move
    // fails the caller's append still runs against the existing (over-cap) file.
    private void RotateIfFull()
    {
        var info = new FileInfo(_path);
        if (!info.Exists || info.Length < _maxBytes)
        {
            return;
        }

        if (File.Exists(BackupPath))
        {
            File.Delete(BackupPath);
        }

        File.Move(_path, BackupPath);
    }
}
