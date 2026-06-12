using System.Windows.Threading;
using Button = System.Windows.Controls.Button;

namespace Blurt.App;

/// <summary>
/// Copy-to-clipboard for the small "Copy link" / "Copy folder" actions
/// (issue 25): puts the text on the clipboard and flashes a short "Copied"
/// confirmation on the button itself, so the manual-install URL and target
/// folder never have to be retyped by hand.
/// </summary>
internal static class ClipboardCopy
{
    public static void WithFeedback(Button button, string text)
    {
        try
        {
            System.Windows.Clipboard.SetText(text);
        }
        catch
        {
            // The clipboard can be transiently locked by another process
            // (CLIPBRD_E_CANT_OPEN). Fail-soft: no crash, the user just clicks
            // again — consistent with the app-wide fail-soft posture (issue 13).
            return;
        }

        var original = button.Content;
        button.Content = "Copied ✓";
        button.IsEnabled = false;

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            button.Content = original;
            button.IsEnabled = true;
        };
        timer.Start();
    }
}
