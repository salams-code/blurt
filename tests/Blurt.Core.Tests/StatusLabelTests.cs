using Blurt.Core;
using Xunit;

namespace Blurt.Core.Tests;

public class StatusLabelTests
{
    [Fact]
    public void Transcribing_distinguishes_local_from_cloud()
    {
        // The local/cloud distinction is the whole point — the user must be able
        // to see whether their voice is leaving the machine.
        Assert.Equal("transcribing locally", StatusLabel.Transcribing(local: true));
        Assert.Equal("transcribing", StatusLabel.Transcribing(local: false));
    }

    [Fact]
    public void Each_refine_mode_has_its_own_verb()
    {
        // Distinct verbs so the pill names exactly what is happening, not a
        // generic "refining" for every mode.
        var verbs = new[]
        {
            StatusLabel.Fixing,
            StatusLabel.Bulleting,
            StatusLabel.Emailing,
            StatusLabel.Translating,
            StatusLabel.Refining,
        };

        Assert.Equal(verbs.Length, verbs.Distinct().Count());
    }

    [Fact]
    public void AlsoEnglish_layers_an_english_marker_on_the_base_verb()
    {
        // Issue 39: the modifier shows as a layered op (e.g. "bulleting → english"),
        // keeping the base verb visible and adding an English marker.
        var layered = StatusLabel.AlsoEnglish(StatusLabel.Bulleting);

        Assert.StartsWith(StatusLabel.Bulleting, layered);
        Assert.Contains("english", layered, System.StringComparison.OrdinalIgnoreCase);
        // Same ellipsis rule as the other labels: the overlay animates the dots.
        Assert.DoesNotContain("…", layered);
        Assert.DoesNotContain(".", layered);
    }

    [Fact]
    public void Labels_carry_no_trailing_ellipsis_so_the_overlay_can_animate_it()
    {
        // The overlay appends the animated dots; baking "…" in would double them.
        foreach (var label in new[]
                 {
                     StatusLabel.Listening,
                     StatusLabel.Transcribing(local: true),
                     StatusLabel.Transcribing(local: false),
                     StatusLabel.Fixing,
                     StatusLabel.Bulleting,
                     StatusLabel.Emailing,
                     StatusLabel.Translating,
                     StatusLabel.Refining,
                 })
        {
            Assert.DoesNotContain("…", label);
            Assert.DoesNotContain(".", label);
        }
    }
}
