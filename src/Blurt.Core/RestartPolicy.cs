namespace Blurt.Core;

/// <summary>
/// Decides whether a settings change only takes effect after relaunching Blurt, so
/// the app can offer an immediate restart instead of silently deferring it (and
/// showing a status line that doesn't match the running backend until then). Today
/// the only such setting is <see cref="BlurtConfig.GpuPreference"/>: it maps to
/// Whisper.net's global-static native load order, which is set once before the first
/// WhisperFactory and cannot change in-process — whisper.cpp loads one native backend
/// per process (ADR-0001).
/// </summary>
public static class RestartPolicy
{
    /// <summary>
    /// True when <paramref name="saved"/> changes a setting that only applies at next
    /// launch, relative to the config the app actually started with
    /// (<paramref name="running"/>). Everything else (sound, hotkeys, prompts, overlay
    /// anchor, transcription source) applies live, so it returns false for those.
    /// </summary>
    public static bool RequiresRestart(BlurtConfig running, BlurtConfig saved) =>
        running.GpuPreference != saved.GpuPreference;
}
