using System.Globalization;

namespace Blurt.Core;

/// <summary>
/// A tiny append-only text log that caps its own size, so a long-running tray app
/// never grows an unbounded file. When the active file reaches
/// <see cref="_maxBytes"/> it is rotated to a single <c>.1</c> backup (the previous
/// backup is dropped), so at most ~2× <see cref="_maxBytes"/> ever sits on disk.
/// Size is the hard ceiling; <see cref="PruneOlderThan"/> adds an age ceiling so the
/// plaintext log doesn't retain usage metadata for the months the size cap allows.
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

    /// <summary>
    /// Drops every log entry older than <paramref name="maxAge"/> from both the
    /// active file and the <c>.1</c> backup, then rewrites them (deleting a file that
    /// ends up empty). Multi-line entries (a crash header plus its stack trace) are
    /// kept or dropped as a whole — only the first line of an entry carries a
    /// timestamp. Meant to run once at startup; like <see cref="Write"/> it
    /// <b>never throws</b>, so a malformed or locked file can't crash launch.
    /// </summary>
    public void PruneOlderThan(TimeSpan maxAge) => PruneOlderThan(maxAge, DateTime.Now);

    /// <summary>
    /// <see cref="PruneOlderThan(TimeSpan)"/> with an explicit <paramref name="now"/>,
    /// so the age cutoff is testable without depending on the wall clock.
    /// </summary>
    public void PruneOlderThan(TimeSpan maxAge, DateTime now)
    {
        lock (_gate)
        {
            try
            {
                var cutoff = now - maxAge;
                PruneFile(_path, cutoff);
                PruneFile(BackupPath, cutoff);
            }
            catch
            {
                // Pruning is housekeeping — a failure must never crash startup.
            }
        }
    }

    // The fixed-width prefix Write stamps before each message; an entry's first line
    // begins with it, continuation lines (stack frames) do not.
    private const string TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff";

    // Rewrite path keeping only entries at or after cutoff. An entry runs from a
    // timestamped line through the continuation lines that follow it, so the whole
    // entry shares one keep/drop decision. Deletes the file if nothing survives.
    private static void PruneFile(string path, DateTime cutoff)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var lines = File.ReadAllLines(path);
        var kept = new List<string>(lines.Length);
        var keepingCurrentEntry = true;   // lines before the first timestamp are kept

        foreach (var line in lines)
        {
            if (TryReadTimestamp(line, out var timestamp))
            {
                keepingCurrentEntry = timestamp >= cutoff;
            }

            if (keepingCurrentEntry)
            {
                kept.Add(line);
            }
        }

        if (kept.Count == 0)
        {
            File.Delete(path);
        }
        else if (kept.Count != lines.Length)
        {
            File.WriteAllText(path, string.Join(Environment.NewLine, kept) + Environment.NewLine);
        }
    }

    private static bool TryReadTimestamp(string line, out DateTime timestamp)
    {
        timestamp = default;
        return line.Length >= TimestampFormat.Length
            && DateTime.TryParseExact(
                line[..TimestampFormat.Length],
                TimestampFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out timestamp);
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
