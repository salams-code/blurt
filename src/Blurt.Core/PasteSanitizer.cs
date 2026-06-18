using System.Text;

namespace Blurt.Core;

/// <summary>
/// Cleans text that is about to be placed on the clipboard and pasted at the
/// focused app's cursor (security finding F2). Provider/refiner output is
/// untrusted: a crafted refinement carrying terminal-control characters or a
/// trailing newline could turn a paste into command execution when the focused
/// window is a shell/REPL.
///
/// The policy is deliberately narrow so it never mangles legitimate dictation:
/// only characters that never come from real speech are removed (ESC, NUL,
/// backspace, bell, and the other C0/C1 controls). Tabs and *internal* newlines
/// are kept so multi-line / indented refinements survive; line endings are
/// normalised to LF, and trailing whitespace is trimmed so a provider-supplied
/// trailing newline cannot auto-submit the final (most dangerous) line.
/// </summary>
public static class PasteSanitizer
{
    public static string Sanitize(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        // Normalise CRLF and lone CR to LF first: a stray '\r' is itself a
        // "submit" in a terminal, and collapsing it here means the control-char
        // filter below only has to reason about '\n'.
        var normalised = text.Replace("\r\n", "\n").Replace('\r', '\n');

        var sb = new StringBuilder(normalised.Length);
        foreach (var c in normalised)
        {
            // Keep newline and tab (legitimate in multi-line / indented text);
            // drop every other control character — ESC, NUL, backspace, bell …
            // turn a paste into terminal control input.
            if (c == '\n' || c == '\t' || !char.IsControl(c))
            {
                sb.Append(c);
            }
        }

        // Trailing whitespace/newline would let the last line auto-submit when the
        // paste lands in a shell. Leading whitespace is harmless, so keep it.
        return sb.ToString().TrimEnd();
    }
}
