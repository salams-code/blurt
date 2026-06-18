namespace Blurt.App;

/// <summary>
/// Builds clipboard data that Windows will not persist in clipboard history or
/// sync to the cloud / other devices (security findings F16/F23). Blurt puts the
/// dictation transcript — and recovery copies of it — on the clipboard; tagging
/// those writes keeps the sensitive text out of the OS clipboard-history database
/// and the cloud clipboard, without clearing anything, so paste at the cursor and
/// manual recovery still work exactly as before. (The same technique password
/// managers use to keep secrets out of clipboard history.)
/// </summary>
internal static class ClipboardPrivacy
{
    public static DataObject TextExcludedFromHistory(string text)
    {
        var data = new DataObject();
        data.SetText(text);

        // A zero DWORD disables each capability; the monitor-exclusion format only
        // needs to be present. These are the documented clipboard format names.
        SetFlag(data, "CanIncludeInClipboardHistory", 0);
        SetFlag(data, "CanUploadToCloudClipboard", 0);
        SetFlag(data, "ExcludeClipboardContentFromMonitorProcessing", 0);
        return data;
    }

    private static void SetFlag(DataObject data, string format, int value)
        => data.SetData(format, new MemoryStream(BitConverter.GetBytes(value)));
}
