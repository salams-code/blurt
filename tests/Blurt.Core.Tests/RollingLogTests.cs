using Blurt.Core;
using Xunit;

namespace Blurt.Core.Tests;

public class RollingLogTests
{
    [Fact]
    public void Write_appends_a_timestamped_line_creating_the_file_and_directory()
    {
        var dir = Directory.CreateTempSubdirectory("blurt-log-").FullName;
        try
        {
            // A nested, not-yet-existing subdir proves Write creates the path.
            var path = Path.Combine(dir, "logs", "blurt.log");
            var log = new RollingLog(path);

            log.Write("hello");
            log.Write("world");

            var lines = File.ReadAllLines(path);
            Assert.Equal(2, lines.Length);
            Assert.EndsWith(" hello", lines[0]);
            Assert.EndsWith(" world", lines[1]);
            // A leading timestamp is present (line is longer than the message).
            Assert.True(lines[0].Length > "hello".Length);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void The_file_rotates_to_a_single_backup_once_it_reaches_the_size_cap()
    {
        var dir = Directory.CreateTempSubdirectory("blurt-log-").FullName;
        try
        {
            var path = Path.Combine(dir, "blurt.log");
            // Tiny cap so a couple of lines trip the rotation.
            var log = new RollingLog(path, maxBytes: 64);

            log.Write(new string('a', 100));   // active file now over the 64-byte cap
            log.Write("after rotation");        // this Write rotates first, then appends

            // The over-cap content moved to the .1 backup; the active file holds
            // only what was written after the rotation.
            Assert.True(File.Exists(log.BackupPath));
            Assert.Contains("aaaa", File.ReadAllText(log.BackupPath));

            var active = File.ReadAllText(path);
            Assert.Contains("after rotation", active);
            Assert.DoesNotContain("aaaa", active);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Only_one_backup_is_kept_across_repeated_rotations()
    {
        var dir = Directory.CreateTempSubdirectory("blurt-log-").FullName;
        try
        {
            var path = Path.Combine(dir, "blurt.log");
            var log = new RollingLog(path, maxBytes: 32);

            // Each oversized write rotates; only the most recent pre-rotation
            // content survives in .1 (the older backup is dropped) and the newest
            // line is in the active file. No .2 ever appears.
            log.Write(new string('1', 40));   // → active
            log.Write(new string('2', 40));   // rotates '1' → .1, '2' → active
            log.Write(new string('3', 40));   // drops '1', rotates '2' → .1, '3' → active

            Assert.True(File.Exists(log.BackupPath));
            Assert.False(File.Exists(path + ".2"));
            Assert.Contains("22222", File.ReadAllText(log.BackupPath));   // previous content
            Assert.Contains("33333", File.ReadAllText(path));             // newest content
            Assert.DoesNotContain("11111", File.ReadAllText(log.BackupPath));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Write_never_throws_even_when_the_path_is_unwritable()
    {
        // The path points at a *file* used as a directory component, so every IO
        // attempt fails. A crash handler relies on Write swallowing that rather
        // than throwing a second exception over the first.
        var dir = Directory.CreateTempSubdirectory("blurt-log-").FullName;
        try
        {
            var blocker = Path.Combine(dir, "blocker");
            File.WriteAllText(blocker, "x");
            var log = new RollingLog(Path.Combine(blocker, "nested", "blurt.log"));

            var ex = Record.Exception(() => log.Write("must not throw"));

            Assert.Null(ex);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void PruneOlderThan_drops_entries_past_the_retention_window_and_keeps_recent_ones()
    {
        var dir = Directory.CreateTempSubdirectory("blurt-log-").FullName;
        try
        {
            var path = Path.Combine(dir, "blurt.log");
            // Two records with explicit timestamps: one old, one recent (the format
            // matches what Write emits — "yyyy-MM-dd HH:mm:ss.fff message").
            File.WriteAllText(
                path,
                "2026-06-01 09:00:00.000 old entry" + Environment.NewLine +
                "2026-06-18 19:00:00.000 recent entry" + Environment.NewLine);
            var log = new RollingLog(path);

            // 14-day window relative to a fixed "now" → cutoff 2026-06-04.
            log.PruneOlderThan(TimeSpan.FromDays(14), new DateTime(2026, 6, 18, 20, 0, 0));

            var text = File.ReadAllText(path);
            Assert.DoesNotContain("old entry", text);
            Assert.Contains("recent entry", text);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void PruneOlderThan_keeps_or_drops_a_multiline_record_as_a_unit()
    {
        // A crash entry spans several lines: only the first carries a timestamp, the
        // stack-trace lines follow without one. Pruning must treat the whole record
        // as a unit — never orphan a kept stack onto a dropped header, or vice versa.
        var dir = Directory.CreateTempSubdirectory("blurt-log-").FullName;
        try
        {
            var path = Path.Combine(dir, "blurt.log");
            File.WriteAllText(
                path,
                "2026-06-01 09:00:00.000 FATAL: old crash" + Environment.NewLine +
                "   at Old.Method()" + Environment.NewLine +
                "2026-06-18 19:00:00.000 FATAL: recent crash" + Environment.NewLine +
                "   at Recent.Method()" + Environment.NewLine);
            var log = new RollingLog(path);

            log.PruneOlderThan(TimeSpan.FromDays(14), new DateTime(2026, 6, 18, 20, 0, 0));

            var text = File.ReadAllText(path);
            Assert.DoesNotContain("old crash", text);
            Assert.DoesNotContain("Old.Method", text);     // continuation of the dropped record
            Assert.Contains("recent crash", text);
            Assert.Contains("Recent.Method", text);          // continuation of the kept record
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void PruneOlderThan_also_prunes_the_backup_file()
    {
        var dir = Directory.CreateTempSubdirectory("blurt-log-").FullName;
        try
        {
            var path = Path.Combine(dir, "blurt.log");
            File.WriteAllText(
                path + ".1",
                "2026-06-01 09:00:00.000 old backup entry" + Environment.NewLine);
            File.WriteAllText(
                path,
                "2026-06-18 19:00:00.000 recent entry" + Environment.NewLine);
            var log = new RollingLog(path);

            log.PruneOlderThan(TimeSpan.FromDays(14), new DateTime(2026, 6, 18, 20, 0, 0));

            // The backup was entirely old → removed; the active file's recent line stays.
            Assert.False(File.Exists(log.BackupPath));
            Assert.Contains("recent entry", File.ReadAllText(path));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void PruneOlderThan_never_throws_when_the_log_does_not_exist()
    {
        // Mirrors Write's contract: prune runs at startup and must never become a
        // crash. A missing file (fresh install) is a no-op, not an exception.
        var dir = Directory.CreateTempSubdirectory("blurt-log-").FullName;
        try
        {
            var log = new RollingLog(Path.Combine(dir, "missing", "blurt.log"));

            var ex = Record.Exception(
                () => log.PruneOlderThan(TimeSpan.FromDays(14), new DateTime(2026, 6, 18, 20, 0, 0)));

            Assert.Null(ex);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
