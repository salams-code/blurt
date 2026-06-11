namespace Blurt.Core;

/// <summary>
/// Seam over the OS clipboard. Snapshots are opaque on purpose: the adapter
/// decides how to capture whatever is on the clipboard (text, images, files…)
/// so the core can hand it back via <see cref="Restore"/> without knowing the
/// representation.
/// </summary>
public interface IClipboard
{
    /// <summary>Captures the current clipboard contents, whatever their format.</summary>
    object? Snapshot();

    /// <summary>Replaces the clipboard contents with plain text.</summary>
    void SetText(string text);

    /// <summary>Puts a previously captured snapshot back on the clipboard.</summary>
    void Restore(object? snapshot);
}

/// <summary>
/// Seam over the simulated paste keystroke (Ctrl+V). Returns false when the
/// keystroke could not be delivered, so the injector knows the paste never
/// reached the focused app.
/// </summary>
public interface IPasteKeystroke
{
    bool SendPaste();
}

/// <summary>
/// Inserts text at the focused app's cursor. The seam that lets the dictation
/// pipeline depend on injection without binding to the concrete
/// <see cref="TextInjector"/>, so it can be faked in tests. Returns false when
/// the text could not be delivered (the paste keystroke failed); the text is
/// then left on the clipboard so it is never silently lost.
/// </summary>
public interface ITextInjector
{
    Task<bool> InjectAsync(string text, CancellationToken ct = default);
}
