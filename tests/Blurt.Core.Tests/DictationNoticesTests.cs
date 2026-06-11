using Blurt.Core;
using Xunit;

namespace Blurt.Core.Tests;

public class DictationNoticesTests
{
    [Fact]
    public void A_successful_injection_is_silent()
    {
        // The text landed at the cursor — surfacing a balloon for every
        // successful dictation would be noise. Injected maps to no notice.
        Assert.Null(DictationNotices.For(DictationOutcome.Injected));
    }

    [Fact]
    public void Nothing_transcribed_is_an_info_notice()
    {
        var notice = DictationNotices.For(DictationOutcome.NothingTranscribed);

        Assert.NotNull(notice);
        Assert.Equal(NoticeLevel.Info, notice!.Level);
        Assert.False(string.IsNullOrWhiteSpace(notice.Message));
    }

    [Fact]
    public void A_transcription_failure_is_an_error_notice()
    {
        var notice = DictationNotices.For(DictationOutcome.TranscriptionFailed);

        Assert.NotNull(notice);
        Assert.Equal(NoticeLevel.Error, notice!.Level);
        Assert.False(string.IsNullOrWhiteSpace(notice.Message));
    }

    [Fact]
    public void Refined_offline_is_a_warning_notice()
    {
        var notice = DictationNotices.For(DictationOutcome.RefinedOffline);

        Assert.NotNull(notice);
        Assert.Equal(NoticeLevel.Warning, notice!.Level);
        Assert.False(string.IsNullOrWhiteSpace(notice.Message));
    }

    [Fact]
    public void Injection_blocked_is_a_warning_notice_mentioning_the_clipboard()
    {
        var notice = DictationNotices.For(DictationOutcome.InjectionBlocked);

        Assert.NotNull(notice);
        Assert.Equal(NoticeLevel.Warning, notice!.Level);
        Assert.False(string.IsNullOrWhiteSpace(notice.Message));
        // The whole point of leaving the text on the clipboard is that the user
        // can paste it; the notice must say so.
        Assert.Contains("clipboard", notice.Message, System.StringComparison.OrdinalIgnoreCase);
    }
}
