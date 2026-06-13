using Blurt.Core;
using Xunit;

namespace Blurt.Core.Tests;

public class TutorialTests
{
    [Fact]
    public void Lesson_opens_with_push_to_talk_then_triggers_before_flex()
    {
        // A newcomer needs the core gesture first, then what the triggers do, then
        // the Flex modes that build on a trigger. Order is the lesson.
        var cards = Tutorial.Cards.ToList();

        Assert.Equal(TutorialCard.PushToTalk, cards[0]);
        Assert.True(cards.IndexOf(TutorialCard.Triggers) < cards.IndexOf(TutorialCard.FlexModes));
    }

    [Fact]
    public void Covers_every_concept_the_issue_requires()
    {
        Assert.Contains(TutorialCard.PushToTalk, Tutorial.Cards);
        Assert.Contains(TutorialCard.Triggers, Tutorial.Cards);
        Assert.Contains(TutorialCard.FlexModes, Tutorial.Cards);
    }

    [Theory]
    [InlineData(TutorialCard.PushToTalk)]
    [InlineData(TutorialCard.Triggers)]
    [InlineData(TutorialCard.FlexModes)]
    [InlineData(TutorialCard.LiveStatus)]
    [InlineData(TutorialCard.TryIt)]
    public void Every_card_has_a_title_and_body(TutorialCard card)
    {
        Assert.False(string.IsNullOrWhiteSpace(Tutorial.Title(card)));
        Assert.False(string.IsNullOrWhiteSpace(Tutorial.Body(card)));
    }

    [Fact]
    public void Card_titles_are_distinct_so_no_two_cards_read_the_same()
    {
        var titles = Tutorial.Cards.Select(Tutorial.Title).ToList();

        Assert.Equal(titles.Count, titles.Distinct().Count());
    }

    [Fact]
    public void Flex_demo_flashes_the_real_shipped_cycle_so_the_lesson_matches_the_app()
    {
        // What they're taught must be exactly what they'll see: same modes, same order.
        Assert.Equal(BlurtConfig.Default.FlexSlotOrder, TutorialDemo.ModeFlashes);
    }

    [Fact]
    public void Status_demo_opens_listening_and_runs_through_transcribing()
    {
        var frames = TutorialDemo.StatusFrames;

        Assert.Equal(StatusLabel.Listening, frames[0]);
        Assert.Contains(StatusLabel.Transcribing(local: false), frames);
        Assert.True(frames.Count >= 2);
    }
}
