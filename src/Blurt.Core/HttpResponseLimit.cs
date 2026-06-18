using System.Text;

namespace Blurt.Core;

/// <summary>
/// Reads an HTTP response body as text with a hard byte cap (security findings
/// F5/F13/F14). A malicious or compromised provider could otherwise return an
/// unbounded body that a streaming JSON read would buffer in full — exhausting
/// memory, and (for refinement) producing a multi-MB "paste bomb" when the result
/// is injected at the cursor. Reading through this cap turns that into a clean
/// failure the dictation pipeline already handles fail-soft (raw transcript).
/// </summary>
public static class HttpResponseLimit
{
    /// <summary>
    /// Generous ceiling for an OpenAI-compatible text response (8 MiB): far above
    /// any real transcript or refinement, far below an out-of-memory.
    /// </summary>
    public const int DefaultMaxBytes = 8 * 1024 * 1024;

    /// <summary>
    /// Reads <paramref name="stream"/> fully as UTF-8 text, but throws
    /// <see cref="InvalidOperationException"/> as soon as more than
    /// <paramref name="maxBytes"/> bytes have arrived — so an oversized body is
    /// never buffered in full.
    /// </summary>
    public static async Task<string> ReadAsStringAsync(
        Stream stream, int maxBytes, CancellationToken ct = default)
    {
        var buffer = new byte[8192];
        using var sink = new MemoryStream();

        int read;
        while ((read = await stream.ReadAsync(buffer, ct)) > 0)
        {
            if (sink.Length + read > maxBytes)
            {
                throw new InvalidOperationException(
                    $"Response body exceeded the {maxBytes}-byte limit.");
            }

            sink.Write(buffer, 0, read);
        }

        return Encoding.UTF8.GetString(sink.ToArray());
    }
}
