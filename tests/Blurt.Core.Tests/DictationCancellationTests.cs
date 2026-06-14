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

        using var dictation = cancellation.Begin();

        Assert.True(cancellation.IsCancellable);
        Assert.False(dictation.Token.IsCancellationRequested);
    }

    [Fact]
    public void RequestCancel_while_in_flight_cancels_the_token_and_reports_consumed()
    {
        var cancellation = new DictationCancellation();
        using var dictation = cancellation.Begin();

        var consumed = cancellation.RequestCancel();

        // True tells the Esc handler the key was used (a dictation was aborted) → swallow it.
        Assert.True(consumed);
        Assert.True(dictation.Token.IsCancellationRequested);
        // Once cancelled the dictation is no longer cancellable.
        Assert.False(cancellation.IsCancellable);
    }

    [Fact]
    public void Second_cancel_is_a_no_op_so_a_second_Esc_passes_through()
    {
        var cancellation = new DictationCancellation();
        using var dictation = cancellation.Begin();
        cancellation.RequestCancel();

        Assert.False(cancellation.RequestCancel());
    }

    [Fact]
    public void Disposing_the_handle_releases_the_dictation_so_a_later_Esc_is_a_no_op()
    {
        var cancellation = new DictationCancellation();
        var dictation = cancellation.Begin();

        dictation.Dispose();

        Assert.False(cancellation.IsCancellable);
        Assert.False(cancellation.RequestCancel());
    }

    [Fact]
    public void A_new_dictation_gets_a_fresh_uncancelled_token()
    {
        var cancellation = new DictationCancellation();
        var first = cancellation.Begin();
        var firstToken = first.Token;
        cancellation.RequestCancel();
        first.Dispose();

        using var second = cancellation.Begin();

        Assert.True(firstToken.IsCancellationRequested);    // the aborted dictation stays cancelled
        Assert.False(second.Token.IsCancellationRequested); // the new one starts clean
        Assert.True(cancellation.IsCancellable);
    }

    [Fact]
    public void A_finishing_dictation_does_not_break_a_newer_overlapping_one()
    {
        // Regression for the fire-and-forget overlap bug: dictations run async, so a
        // second can begin while the first is still transcribing. The first finishing
        // (disposing its handle) must NOT clear the newer one's cancellability, and Esc
        // must still cancel the newer (in-flight) dictation — not leak to the app.
        var cancellation = new DictationCancellation();
        var first = cancellation.Begin();
        var firstToken = first.Token;
        var second = cancellation.Begin();   // B starts while A is still in flight
        var secondToken = second.Token;

        first.Dispose();                     // A finishes first

        Assert.True(cancellation.IsCancellable);              // B is still cancellable
        Assert.True(cancellation.RequestCancel());            // Esc cancels B (and is swallowed)
        Assert.True(secondToken.IsCancellationRequested);     // B's token tripped
        Assert.False(firstToken.IsCancellationRequested);     // A was never cancelled by Esc

        second.Dispose();
    }

    [Fact]
    public void Cancel_targets_the_most_recent_in_flight_dictation()
    {
        var cancellation = new DictationCancellation();
        using var first = cancellation.Begin();
        using var second = cancellation.Begin();

        cancellation.RequestCancel();

        Assert.True(second.Token.IsCancellationRequested);
        Assert.False(first.Token.IsCancellationRequested);
    }
}
