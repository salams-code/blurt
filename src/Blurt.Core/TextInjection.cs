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
