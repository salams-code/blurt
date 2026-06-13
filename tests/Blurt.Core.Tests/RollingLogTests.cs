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
}
