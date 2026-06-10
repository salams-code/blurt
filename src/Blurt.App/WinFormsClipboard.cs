using Blurt.Core;

namespace Blurt.App;

/// <summary>
/// WinForms <see cref="Clipboard"/> adapter for <see cref="IClipboard"/>. Must
/// be called on the tray app's UI thread (STA — WinForms clipboard access
/// throws otherwise), which is where the keyboard hook raises triggers.
///
/// Verified manually on Windows; all decision logic lives in
/// <see cref="TextInjector"/> in the core.
/// </summary>
internal sealed class WinFormsClipboard : IClipboard
{
    /// <summary>
    /// Copies the current clipboard contents — every format, not just text —
    /// into a detached <see cref="DataObject"/>. A plain
    /// <c>Clipboard.GetDataObject()</c> result is only a proxy onto the live
    /// clipboard, so it would be invalidated the moment we overwrite it with
    /// the dictated text; copying the data out makes the snapshot survive.
    /// </summary>
    public object? Snapshot()
    {
        var current = Clipboard.GetDataObject();
        if (current is null)
        {
            return null;   // empty clipboard — restoring this means clearing it
        }

        var copy = new DataObject();
        foreach (var format in current.GetFormats(autoConvert: false))
        {
            try
            {
                if (current.GetDataPresent(format, autoConvert: false))
                {
                    copy.SetData(format, current.GetData(format, autoConvert: false));
                }
            }
            catch
            {
                // Some formats fail to render (delayed rendering from a dead
                // source app, exotic OLE formats). Best effort: keep the rest.
            }
        }

        return copy;
    }

    public void SetText(string text)
        => Clipboard.SetDataObject(text, copy: true);

    public void Restore(object? snapshot)
    {
        if (snapshot is IDataObject data)
        {
            Clipboard.SetDataObject(data, copy: true);
        }
        else
        {
            Clipboard.Clear();   // the snapshot was an empty clipboard
        }
    }
}
