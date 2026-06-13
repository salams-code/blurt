namespace Blurt.App;

internal static class Program
{
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

        ApplicationConfiguration.Initialize();
        using var context = new TrayApplicationContext();
        Application.Run(context);
    }
}
