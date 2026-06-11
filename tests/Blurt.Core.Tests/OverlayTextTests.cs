using Blurt.Core;
using Xunit;

namespace Blurt.Core.Tests;

public class OverlayTextTests
{
    [Fact]
    public void Listening_shows_a_listening_label()
    {
        var text = OverlayText.For(OverlayState.Listening);

        Assert.False(string.IsNullOrWhiteSpace(text));
        Assert.Contains("listening", text, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Transcribing_shows_a_transcribing_label()
    {
        var text = OverlayText.For(OverlayState.Transcribing);

        Assert.False(string.IsNullOrWhiteSpace(text));
        Assert.Contains("transcribing", text, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Hidden_has_no_text()
    {
        // The pill is invisible when hidden, so it has nothing to say.
        Assert.Equal("", OverlayText.For(OverlayState.Hidden));
    }
}
