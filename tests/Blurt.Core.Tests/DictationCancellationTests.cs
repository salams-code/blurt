using Blurt.Core;
using Xunit;

namespace Blurt.Core.Tests;

public class DictationCancellationTests
{
    [Fact]
    public void Fresh_coordinator_has_nothing_to_cancel()
    {
        var cancellation = new DictationCancellation();

        Assert.False(cancellation.IsCancellable);
        // Esc with no dictation in flight is a no-op, so the hook must NOT swallow it.
        Assert.False(cancellation.RequestCancel());
    }

    [Fact]
    public void Begin_marks_a_dictation_cancellable_with_a_live_token()
    {
        var cancellation = new DictationCancellation();

        var token = cancellation.Begin();

        Assert.True(cancellation.IsCancellable);
        Assert.False(token.IsCancellationRequested);
    }

    [Fact]
    public void RequestCancel_while_in_flight_cancels_the_token_and_reports_consumed()
    {
        var cancellation = new DictationCancellation();
        var token = cancellation.Begin();

        var consumed = cancellation.RequestCancel();

        // True tells the Esc handler the key was used (a dictation was aborted) → swallow it.
        Assert.True(consumed);
        Assert.True(token.IsCancellationRequested);
        // Once cancelled the dictation is no longer cancellable.
        Assert.False(cancellation.IsCancellable);
    }

    [Fact]
    public void Second_cancel_is_a_no_op_so_a_second_Esc_passes_through()
    {
        var cancellation = new DictationCancellation();
        cancellation.Begin();
        cancellation.RequestCancel();

        Assert.False(cancellation.RequestCancel());
    }

    [Fact]
    public void End_releases_the_dictation_so_a_later_Esc_is_a_no_op()
    {
        var cancellation = new DictationCancellation();
        cancellation.Begin();

        cancellation.End();

        Assert.False(cancellation.IsCancellable);
        Assert.False(cancellation.RequestCancel());
    }

    [Fact]
    public void A_new_dictation_gets_a_fresh_uncancelled_token()
    {
        var cancellation = new DictationCancellation();
        var first = cancellation.Begin();
        cancellation.RequestCancel();
        cancellation.End();

        var second = cancellation.Begin();

        Assert.True(first.IsCancellationRequested);    // the aborted dictation stays cancelled
        Assert.False(second.IsCancellationRequested);  // the new one starts clean
        Assert.True(cancellation.IsCancellable);
    }
}
