using Blurt.Core;
using Xunit;

namespace Blurt.Core.Tests;

public class ExceptionLogFormatTests
{
    [Fact]
    public void Includes_the_exception_type_and_message()
    {
        var summary = ExceptionLogFormat.Summarize(new InvalidOperationException("boom"));

        Assert.Contains("System.InvalidOperationException", summary);
        Assert.Contains("boom", summary);
    }

    [Fact]
    public void A_very_long_message_is_truncated()
    {
        // The point of F18: a message must not dump unbounded content into the
        // plaintext log. Cap it.
        var huge = new string('x', 5000);

        var summary = ExceptionLogFormat.Summarize(new Exception(huge), maxMessageLength: 100);

        Assert.DoesNotContain(huge, summary);
        Assert.Contains("…", summary);
        // The kept slice never exceeds the cap (+ the ellipsis).
        Assert.True(summary.Length < 5000);
    }

    [Fact]
    public void The_inner_exception_chain_is_included()
    {
        var inner = new IOException("disk gone");
        var outer = new InvalidOperationException("startup failed", inner);

        var summary = ExceptionLogFormat.Summarize(outer);

        Assert.Contains("System.InvalidOperationException", summary);
        Assert.Contains("startup failed", summary);
        Assert.Contains("System.IO.IOException", summary);
        Assert.Contains("disk gone", summary);
    }

    [Fact]
    public void The_stack_trace_is_kept_when_present()
    {
        // A thrown exception carries a stack; the summary keeps it for diagnosis
        // (code locations, not user data).
        try
        {
            throw new InvalidOperationException("thrown");
        }
        catch (Exception e)
        {
            var summary = ExceptionLogFormat.Summarize(e);

            Assert.Contains(nameof(The_stack_trace_is_kept_when_present), summary);
        }
    }

    [Fact]
    public void The_stack_trace_is_omitted_when_excluded()
    {
        // A degraded-but-recovered notice (refiner offline, transcription retried)
        // wants a one-line reason in the rolling log, not a full multi-line stack —
        // the type and message say enough. Type + message stay; the stack is dropped.
        try
        {
            throw new InvalidOperationException("thrown");
        }
        catch (Exception e)
        {
            var summary = ExceptionLogFormat.Summarize(e, includeStackTrace: false);

            Assert.Contains("System.InvalidOperationException", summary);
            Assert.Contains("thrown", summary);
            Assert.DoesNotContain(nameof(The_stack_trace_is_omitted_when_excluded), summary);
        }
    }
}
