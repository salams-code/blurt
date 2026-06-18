using System.Text;

namespace Blurt.Core;

/// <summary>
/// Renders an exception for the crash log as a curated summary — type names, a
/// length-capped message per exception, and stack traces — instead of the raw
/// <see cref="Exception.ToString"/> (security finding F18). ToString() serialises
/// the full message of every inner exception verbatim; capping each message
/// bounds an accidental dump of sensitive data a message might one day carry into
/// the plaintext log, while keeping the type and stack that make a crash
/// diagnosable.
/// </summary>
public static class ExceptionLogFormat
{
    /// <summary>Per-exception message cap; longer messages are truncated with an ellipsis.</summary>
    public const int DefaultMaxMessageLength = 500;

    /// <summary>
    /// A curated, log-safe rendering of <paramref name="exception"/> and its inner
    /// chain. Each exception contributes its type, a message truncated to
    /// <paramref name="maxMessageLength"/>, and its stack trace (when present).
    /// </summary>
    public static string Summarize(Exception exception, int maxMessageLength = DefaultMaxMessageLength)
    {
        var sb = new StringBuilder();
        Exception? current = exception;
        var depth = 0;

        while (current is not null)
        {
            if (depth > 0)
            {
                sb.Append(" ---> ");
            }

            sb.Append(current.GetType().FullName)
              .Append(": ")
              .Append(Truncate(current.Message, maxMessageLength));

            if (!string.IsNullOrEmpty(current.StackTrace))
            {
                sb.Append(Environment.NewLine).Append(current.StackTrace);
            }

            sb.Append(Environment.NewLine);
            current = current.InnerException;
            depth++;
        }

        return sb.ToString().TrimEnd();
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";
}
