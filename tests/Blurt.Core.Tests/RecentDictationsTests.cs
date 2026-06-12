using Blurt.Core;
using Xunit;

namespace Blurt.Core.Tests;

public class RecentDictationsTests
{
    [Fact]
    public void A_fresh_history_is_empty()
    {
        // Privacy contract (issue 26): nothing is persisted, so a new instance —
        // i.e. every app launch — starts with no entries.
        var history = new RecentDictations();

        Assert.Empty(history.Items);
    }

    [Fact]
    public void Added_results_are_listed_newest_first()
    {
        // The tray submenu shows the most recent result at the top — that's the
        // one a void-paste recovery almost always wants.
        var history = new RecentDictations();

        history.Add("first");
        history.Add("second");

        Assert.Equal(new[] { "second", "first" }, history.Items);
    }

    [Fact]
    public void The_fourth_result_evicts_the_oldest()
    {
        // Fixed capacity of 3 (the user's ask): adding beyond it drops the
        // oldest entry, never grows the list.
        var history = new RecentDictations();

        history.Add("one");
        history.Add("two");
        history.Add("three");
        history.Add("four");

        Assert.Equal(new[] { "four", "three", "two" }, history.Items);
    }

    [Fact]
    public void Blank_results_are_not_recorded()
    {
        // An empty transcription (silence, mic glitch) injects nothing worth
        // recovering — a blank menu entry would only push out a real one.
        var history = new RecentDictations();

        history.Add("");
        history.Add("   ");
        history.Add("real");

        Assert.Equal(new[] { "real" }, history.Items);
    }

    [Fact]
    public void Preview_returns_short_text_unchanged()
    {
        Assert.Equal("Kurzer Satz.", RecentDictations.Preview("Kurzer Satz."));
    }

    [Fact]
    public void Preview_truncates_long_text_with_an_ellipsis()
    {
        var longText = new string('x', 100);

        var preview = RecentDictations.Preview(longText);

        // A menu item must stay one readable line: hard cap with a visible "…"
        // so the user knows there's more.
        Assert.Equal(48, preview.Length);
        Assert.EndsWith("…", preview);
        Assert.StartsWith("xxx", preview);
    }

    [Fact]
    public void Preview_collapses_newlines_and_runs_of_whitespace()
    {
        // Bullets-mode output is multi-line; a menu label must not be.
        var multiline = "- erstens\n- zweitens\r\n\t- drittens";

        Assert.Equal("- erstens - zweitens - drittens",
            RecentDictations.Preview(multiline));
    }
}
