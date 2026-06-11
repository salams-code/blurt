namespace Blurt.Core;

/// <summary>
/// Inserts text at the focused app's own cursor by going through the clipboard:
/// snapshot what's there → put the text on it → simulate Ctrl+V → restore the
/// snapshot. No caret querying; the focused app does the actual insertion.
/// </summary>
public sealed class TextInjector : ITextInjector
{
    private readonly IClipboard _clipboard;
    private readonly IPasteKeystroke _paste;
    private readonly Func<Task> _postPasteDelay;

    public TextInjector(IClipboard clipboard, IPasteKeystroke paste, Func<Task> postPasteDelay)
    {
        _clipboard = clipboard;
        _paste = paste;
        _postPasteDelay = postPasteDelay;
    }

    /// <summary>
    /// Injects <paramref name="text"/> into the focused app. Returns false when
    /// the paste keystroke could not be delivered; in that case the text is left
    /// on the clipboard so the user can paste it manually — never silently lost.
    /// </summary>
    public async Task<bool> InjectAsync(string text, CancellationToken ct = default)
    {
        // The snapshot is best-effort: losing the user's clipboard backup is
        // bad, but losing the dictated text is worse. If the clipboard can't be
        // read (locked by another process, etc.), inject anyway and skip the
        // restore — there is nothing trustworthy to put back.
        object? snapshot = null;
        var haveSnapshot = false;
        try
        {
            snapshot = _clipboard.Snapshot();
            haveSnapshot = true;
        }
        catch
        {
        }

        _clipboard.SetText(text);
        if (!_paste.SendPaste())
        {
            return false;
        }

        // The focused app reads the clipboard asynchronously after Ctrl+V;
        // restoring immediately would make it paste the *old* contents.
        await _postPasteDelay();
        if (haveSnapshot)
        {
            _clipboard.Restore(snapshot);
        }

        return true;
    }
}
