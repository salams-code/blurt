using System;
using Blurt.Core;
using Xunit;

namespace Blurt.Core.Tests;

public class TapHoldClassifierTests
{
    [Fact]
    public void A_duration_below_the_threshold_is_a_tap()
    {
        var classifier = new TapHoldClassifier(TimeSpan.FromMilliseconds(250));

        Assert.Equal(TapOrHold.Tap, classifier.Classify(TimeSpan.FromMilliseconds(100)));
    }

    [Fact]
    public void A_duration_above_the_threshold_is_a_hold()
    {
        var classifier = new TapHoldClassifier(TimeSpan.FromMilliseconds(250));

        Assert.Equal(TapOrHold.Hold, classifier.Classify(TimeSpan.FromMilliseconds(400)));
    }

    [Fact]
    public void A_duration_exactly_at_the_threshold_is_a_hold()
    {
        // Boundary is defined as: < threshold = tap, >= threshold = hold.
        var classifier = new TapHoldClassifier(TimeSpan.FromMilliseconds(250));

        Assert.Equal(TapOrHold.Hold, classifier.Classify(TimeSpan.FromMilliseconds(250)));
    }

    [Fact]
    public void The_default_threshold_is_250_milliseconds()
    {
        var classifier = new TapHoldClassifier();

        Assert.Equal(TapOrHold.Tap, classifier.Classify(TimeSpan.FromMilliseconds(249)));
        Assert.Equal(TapOrHold.Hold, classifier.Classify(TimeSpan.FromMilliseconds(250)));
    }

    [Fact]
    public void The_threshold_is_configurable()
    {
        // A longer threshold reclassifies a duration that would be a hold under
        // the default as a tap — proving the boundary is injected, not baked in.
        var classifier = new TapHoldClassifier(TimeSpan.FromMilliseconds(500));

        Assert.Equal(TapOrHold.Tap, classifier.Classify(TimeSpan.FromMilliseconds(400)));
        Assert.Equal(TapOrHold.Hold, classifier.Classify(TimeSpan.FromMilliseconds(500)));
    }
}
