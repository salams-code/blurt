using Blurt.Core;

namespace Blurt.App;

internal static class Program
{
    // How long a log entry is kept before startup prunes it (the size cap is the
    // separate hard ceiling). 14 days is enough history to diagnose a recent issue.
    private const int LogRetentionDays = 14;

    [STAThread]
    private static void Main()
    {
        // Headless native-whisper diagnostic for the portable build — runs before
        // any tray/UI/hook setup and exits. See SelfTest.
        if (Environment.GetCommandLineArgs().Contains("--selftest"))
        {
            SelfTest.Run();
            return;
        }

        // Dev-only: (re)generate the window/exe icon from the brand mark, then exit.
        // Runs before any UI/tray. Pass the output path, e.g.
        //   Blurt.exe --export-icon src\Blurt.App\assets\blurt.ico
        var cliArgs = Environment.GetCommandLineArgs();
        var iconIdx = Array.IndexOf(cliArgs, "--export-icon");
        if (iconIdx >= 0)
        {
            BlurtLogo.ExportBrandIco(iconIdx + 1 < cliArgs.Length ? cliArgs[iconIdx + 1] : "blurt.ico");
            return;
        }

        // Crash capture: until now an unhandled exception left nothing to inspect.
        // Wire a self-rotating log and route every unhandled-exception channel to
        // it before anything else can throw, so a tester's crash is recoverable.
        var log = InstallCrashLog();

        try
        {
            ApplicationConfiguration.Initialize();
            using var context = new TrayApplicationContext(log);
            Application.Run(context);
        }
        catch (Exception ex)
        {
            // Anything escaping startup or the message loop: record it, then let it
            // surface (rethrow) rather than vanish. F18: log a curated summary, not
            // the raw exception, so the plaintext log never dumps unbounded content.
            log.Write($"FATAL (Main): {ExceptionLogFormat.Summarize(ex)}");
            throw;
        }
    }

    // Set up the rotating log under %APPDATA%\Blurt\logs\blurt.log (next to the
    // models folder) and subscribe the three unhandled-exception sources that can
    // tear the app down: the AppDomain (any thread), the WinForms UI thread, and
    // unobserved faults from the app's fire-and-forget dictation tasks.
    private static RollingLog InstallCrashLog()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppInfo.Name, "logs", "blurt.log");
        var log = new RollingLog(path);

        // Drop entries older than the retention window before this session starts.
        // The log is already size-capped (~1 MB), but at typical use that's months of
        // history; an age cap keeps usage metadata from lingering. Best-effort — the
        // log only holds timings/lifecycle/diagnostics, never dictated text.
        log.PruneOlderThan(TimeSpan.FromDays(LogRetentionDays));

        var version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "?";
        log.Write($"=== {AppInfo.Name} {version} started (pid {Environment.ProcessId}) ===");

        // F18: every sink logs a curated summary (type + capped message + stack)
        // rather than the raw exception's full ToString(), so a message can't dump
        // unbounded/sensitive content into the plaintext log.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            log.Write($"FATAL (AppDomain, terminating={e.IsTerminating}): " +
                      $"{(e.ExceptionObject as Exception is { } ax ? ExceptionLogFormat.Summarize(ax) : e.ExceptionObject)}");

        // CatchException makes WinForms raise ThreadException for UI-thread faults
        // (instead of its own dialog), so we log them; the app then keeps running,
        // matching the design's fail-soft intent.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
            log.Write($"ERROR (UI thread): {ExceptionLogFormat.Summarize(e.Exception)}");

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            log.Write($"ERROR (unobserved task): {ExceptionLogFormat.Summarize(e.Exception)}");
            e.SetObserved();
        };

        return log;
    }
}
