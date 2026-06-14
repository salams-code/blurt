namespace Blurt.Core;

/// <summary>
/// Decides whether a settings change only takes effect after relaunching Blurt, so
/// the app can offer an immediate restart (naming what changed) instead of silently
/// deferring it. Two settings are restart-only:
/// <list type="bullet">
/// <item><see cref="BlurtConfig.GpuPreference"/> — maps to Whisper.net's global-static
/// native load order, set once before the first WhisperFactory; whisper.cpp loads one
/// native backend per process, so it can't change in-process (ADR-0001).</item>
/// <item><see cref="BlurtConfig.WhisperModel"/> — the ggml model is memory-mapped into
/// a cached factory at the warmup probe / first dictation and kept for the process
/// lifetime, so switching it also only applies after a relaunch.</item>
/// </list>
/// Everything else (sound, hotkeys, prompts, overlay anchor, transcription source)
/// applies live.
/// </summary>
public static class RestartPolicy
{
    /// <summary>
    /// The human-facing names of the restart-only settings that differ between the
    /// config the app started with (<paramref name="running"/>) and the just-saved
    /// <paramref name="saved"/> — empty when nothing restart-relevant changed. Used to
    /// tell the user exactly what needs the relaunch.
    /// </summary>
    public static IReadOnlyList<string> RestartRequiredChanges(BlurtConfig running, BlurtConfig saved)
    {
        var changes = new List<string>();

        if (running.GpuPreference != saved.GpuPreference)
        {
            changes.Add("GPU acceleration");
        }

        if (running.WhisperModel != saved.WhisperModel)
        {
            changes.Add("local model");
        }

        return changes;
    }

    /// <summary>
    /// True when <paramref name="saved"/> changes any restart-only setting relative to
    /// the config the app actually started with (<paramref name="running"/>).
    /// </summary>
    public static bool RequiresRestart(BlurtConfig running, BlurtConfig saved) =>
        RestartRequiredChanges(running, saved).Count > 0;
}
