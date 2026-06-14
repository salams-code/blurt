using System.Linq;
using Blurt.Core;
using Whisper.net;
using Whisper.net.LibraryLoader;

namespace Blurt.App;

/// <summary>
/// Headless diagnostic (<c>Blurt.exe --selftest</c>): proves the native
/// whisper.cpp libraries load in <em>this</em> build before anyone relies on a
/// dictation, and reports <em>which backend</em> loaded (Vulkan GPU vs CPU). That
/// is the one real risk of the single-file portable exe, where the native libs sit
/// next to the managed bundle rather than being self-extracted — so it must be
/// checked on the shipped artifact, not just a normal build (ADR-0001, issue 46).
///
/// Loads whatever ggml model is already installed (no download) under the same
/// Vulkan-preferred load order the app uses (issue 42), creates a
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

            // Exercise the same Vulkan-preferred order the app sets at startup (issue
            // 42), so the smoke test loads the GPU backend when the box supports it and
            // falls back to CPU otherwise — exactly the runtime path users get.
            RuntimeOptions.RuntimeLibraryOrder = WhisperBackend.OrderFor(GpuPreference.Auto).ToList();

            // Diagnose where the native whisper libs land at runtime. The CPU runtime
            // (Whisper.net.Runtime) lands under runtimes/win-x64/; the Vulkan runtime
            // (Whisper.net.Runtime.Vulkan, issue 46) lands under the DIFFERENT
            // runtimes/vulkan/win-x64/ — including the ~55 MB ggml-vulkan-whisper.dll.
            // Whisper.net's loader probes both from AppContext.BaseDirectory, so no
            // explicit LibraryPath override is needed when the natives sit on disk
            // (IncludeNativeLibrariesForSelfExtract=false).
            var baseDir = AppContext.BaseDirectory;
            var natives = new (string Label, string Path)[]
            {
                ("cpu", Path.Combine(baseDir, "runtimes", "win-x64", "whisper.dll")),
                ("vulkan", Path.Combine(baseDir, "runtimes", "vulkan", "win-x64", "whisper.dll")),
                ("vulkan-ggml", Path.Combine(baseDir, "runtimes", "vulkan", "win-x64", "ggml-vulkan-whisper.dll")),
                ("loose", Path.Combine(baseDir, "whisper.dll")),
            };
            var probe = string.Join("; ", natives.Select(n => $"{n.Label}={(File.Exists(n.Path) ? "Y" : "n")}"));

            // FromPath + Build is the full native surface; if the libs didn't load
            // this throws (DllNotFoundException / BadImageFormatException).
            using var factory = WhisperFactory.FromPath(model);
            using var _ = factory.CreateBuilder().WithLanguage("auto").Build();

            // Which native backend actually loaded — the whole point of issue 46.
            var loaded = RuntimeOptions.LoadedLibrary;
            var backend = loaded is { } lib ? $"{WhisperBackend.Active(lib)} ({lib})" : "<unknown>";

            Write(log, $"PASS: native whisper loaded | backend={backend} | baseDir={baseDir} | probe[{probe}]", exitCode: 0);
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
                        .Select(p => p.Replace(baseDir, "")).Take(20));
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
