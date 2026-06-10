using Blurt.Core;
using Xunit;

namespace Blurt.Core.Tests;

public class TextInjectorTests
{
    [Fact]
    public async Task Injecting_sets_the_clipboard_to_the_text_and_sends_the_paste_keystroke()
    {
        var clipboard = new FakeClipboard();
        var paste = new FakePasteKeystroke();
        var injector = new TextInjector(clipboard, paste, postPasteDelay: () => Task.CompletedTask);

        await injector.InjectAsync("hello from blurt");

        Assert.Equal("hello from blurt", clipboard.LastSetText);
        Assert.True(paste.PasteSent);
    }

    [Fact]
    public async Task Original_clipboard_is_restored_but_only_after_the_post_paste_delay_elapses()
    {
        var clipboard = new FakeClipboard();
        var paste = new FakePasteKeystroke();
        var delay = new TaskCompletionSource();   // the delay seam, completed by the test
        var injector = new TextInjector(clipboard, paste, postPasteDelay: () => delay.Task);

        var injection = injector.InjectAsync("hello from blurt");

        // Paste is asynchronous in the target app: restoring now would paste the
        // *old* clipboard. So before the delay elapses, nothing may be restored.
        Assert.True(paste.PasteSent);
        Assert.False(clipboard.RestoreCalled);

        delay.SetResult();
        await injection;

        Assert.True(clipboard.RestoreCalled);
        Assert.Same(clipboard.SnapshotContents, clipboard.RestoredSnapshot);
    }

    [Fact]
    public async Task When_the_paste_keystroke_fails_the_text_stays_on_the_clipboard()
    {
        var clipboard = new FakeClipboard();
        var paste = new FakePasteKeystroke { Result = false };
        var injector = new TextInjector(clipboard, paste, postPasteDelay: () => Task.CompletedTask);

        var injected = await injector.InjectAsync("hello from blurt");

        // The paste never reached the app, so the dictated text must not be
        // silently lost: leave it on the clipboard instead of restoring.
        Assert.False(injected);
        Assert.False(clipboard.RestoreCalled);
        Assert.Equal("hello from blurt", clipboard.LastSetText);
    }

    [Fact]
    public async Task When_the_clipboard_snapshot_fails_injection_still_proceeds_without_a_restore()
    {
        var clipboard = new FakeClipboard { SnapshotThrows = true };
        var paste = new FakePasteKeystroke();
        var injector = new TextInjector(clipboard, paste, postPasteDelay: () => Task.CompletedTask);

        var injected = await injector.InjectAsync("hello from blurt");

        // Losing the user's clipboard backup is bad; losing the dictation is
        // worse. Paste anyway, and skip the restore (there is nothing to put back).
        Assert.True(injected);
        Assert.Equal("hello from blurt", clipboard.LastSetText);
        Assert.True(paste.PasteSent);
        Assert.False(clipboard.RestoreCalled);
    }

    /// <summary>
    /// Hand-rolled fake over the clipboard seam; records calls so tests can
    /// assert on what the injector did and in what order.
    /// </summary>
    private sealed class FakeClipboard : IClipboard
    {
        public object SnapshotContents { get; } = new();
        public bool SnapshotThrows { get; init; }
        public string? LastSetText { get; private set; }
        public object? RestoredSnapshot { get; private set; }
        public bool RestoreCalled { get; private set; }

        public object? Snapshot()
            => SnapshotThrows
                ? throw new InvalidOperationException("clipboard unavailable")
                : SnapshotContents;

        public void SetText(string text) => LastSetText = text;

        public void Restore(object? snapshot)
        {
            RestoreCalled = true;
            RestoredSnapshot = snapshot;
        }
    }

    private sealed class FakePasteKeystroke : IPasteKeystroke
    {
        public bool Result { get; set; } = true;
        public bool PasteSent { get; private set; }

        public bool SendPaste()
        {
            PasteSent = true;
            return Result;
        }
    }
}
