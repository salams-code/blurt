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
    /// chain. Each exception contributes its type and a message truncated to
    /// <paramref name="maxMessageLength"/>; the stack trace (when present) is
    /// appended only when <paramref name="includeStackTrace"/> is set. Crash logging
    /// keeps the stack (the default); a degraded-but-recovered notice (refiner
    /// offline, transcription retried) drops it for a compact one-line reason — the
    /// type and message already say what failed.
    /// </summary>
    public static string Summarize(
        Exception exception,
        int maxMessageLength = DefaultMaxMessageLength,
        bool includeStackTrace = true)
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

            if (includeStackTrace && !string.IsNullOrEmpty(current.StackTrace))
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
