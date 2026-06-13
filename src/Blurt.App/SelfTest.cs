using System.Linq;
using Blurt.Core;
using Whisper.net;
using Whisper.net.LibraryLoader;

namespace Blurt.App;

/// <summary>
/// Headless diagnostic (<c>Blurt.exe --selftest</c>): proves the native
/// whisper.cpp libraries load in <em>this</em> build before anyone relies on a
/// dictation. That is the one real risk of the single-file portable exe, where
/// the native libs are self-extracted to a temp folder at startup rather than
/// sitting next to the managed assemblies — so it must be checked on the shipped
/// artifact, not just a normal build.
///
/// Loads whatever ggml model is already installed (no download), creates a
/// <see cref="WhisperFactory"/> — the call that P/Invokes into whisper.cpp — and
/// writes PASS / FAIL / SKIP to <c>%TEMP%\blurt-selftest.txt</c> with a matching
/// exit code (0/1/2), then returns without starting the tray.
/// </summary>
internal static class SelfTest
{
    public static void Run()
    {
        var log = Path.Combine(Path.GetTempPath(), "blurt-selftest.txt");
        try
        {
            var provisioner = new ModelProvisioner(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                NullDownloader.Instance);
            var dir = provisioner.ModelsDirectory;

            var model = Directory.Exists(dir)
                ? Directory.EnumerateFiles(dir, "*.bin").FirstOrDefault()
                : null;
            if (model is null)
            {
                Write(log, $"SKIP: no ggml model in {dir} — install one, then re-run --selftest.", exitCode: 2);
                return;
            }

            // Diagnose where the native whisper.dll actually lands at runtime. In a
            // single-file self-extract build AppContext.BaseDirectory is the extraction
            // dir; Whisper.net probes a runtimes/<rid>/native layout under it.
            var baseDir = AppContext.BaseDirectory;
            var candidates = new[]
            {
                Path.Combine(baseDir, "whisper.dll"),
                Path.Combine(baseDir, "runtimes", "win-x64", "native", "whisper.dll"),
            };
            var found = candidates.FirstOrDefault(File.Exists);
            var probe = string.Join("; ", candidates.Select(c => $"{c}={(File.Exists(c) ? "Y" : "n")}"));

            // If we located it, point Whisper.net straight at it (Context7: RuntimeOptions.LibraryPath).
            if (found is not null)
                RuntimeOptions.LibraryPath = found;

            // FromPath + Build is the full native surface; if the libs didn't load
            // this throws (DllNotFoundException / BadImageFormatException).
            using var factory = WhisperFactory.FromPath(model);
            using var _ = factory.CreateBuilder().WithLanguage("auto").Build();

            Write(log, $"PASS: native whisper loaded (LibraryPath={found ?? "<default probe>"}) | baseDir={baseDir} | probe[{probe}]", exitCode: 0);
        }
        catch (Exception ex)
        {
            // List any whisper-ish natives under the base dir to see where they landed.
            var baseDir = AppContext.BaseDirectory;
            string nativesSeen;
            try
            {
                nativesSeen = string.Join(", ",
                    Directory.EnumerateFiles(baseDir, "*whisper*.dll", SearchOption.AllDirectories)
                        .Select(p => p.Replace(baseDir, "")).Take(10));
            }
            catch { nativesSeen = "(enumerate failed)"; }

            Write(log, $"FAIL: {ex.GetType().Name}: {ex.Message} | baseDir={baseDir} | whisper natives found: [{nativesSeen}]", exitCode: 1);
        }
    }

    private static void Write(string path, string message, int exitCode)
    {
        try { File.WriteAllText(path, message + Environment.NewLine); }
        catch { /* diagnostic best-effort; the exit code still carries the result */ }
        Environment.ExitCode = exitCode;
    }

    // The provisioner only needs a downloader to construct; --selftest never
    // downloads (it tests an already-installed model), so a no-op stand-in suffices.
    private sealed class NullDownloader : IModelDownloader
    {
        public static readonly NullDownloader Instance = new();
        public Task DownloadAsync(WhisperModel model, string targetPath, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
