namespace Blurt.Core;

/// <summary>
/// Severity of a fail-soft notice, kept separate from any UI concept (tray
/// icon, overlay colour) so Core can decide *what* a notice means while the App
/// adapter decides how to show it.
/// </summary>
public enum NoticeLevel
{
    /// <summary>Informational — nothing went wrong (e.g. "no speech detected").</summary>
    Info,

    /// <summary>A degraded but recovered outcome (e.g. refinement offline, paste blocked).</summary>
    Warning,

    /// <summary>An operation failed outright (e.g. transcription error).</summary>
    Error,
}

/// <summary>
/// The single fail-soft surface every part of Blurt uses to tell the user
/// something — instead of scattered, blocking dialogs. The concrete adapter
/// (App's <c>TrayNotifier</c>) renders it on the tray and, from issue 06, the
/// overlay. Keeping this a seam in Core lets the pure logic decide *that* a
/// notice is due without depending on WinForms.
/// </summary>
public interface INotifier
{
    /// <summary>
    /// Surfaces <paramref name="message"/> at the given <paramref name="level"/>.
    /// Must never block (no modal dialogs) and must never throw — a failing
    /// notice can't be allowed to crash the app it is reporting on.
    /// </summary>
    void Notify(string message, NoticeLevel level);
}

/// <summary>
/// A user-facing notice: the text to show and how loudly. Returned by
/// <see cref="DictationNotices.For"/>; a null result means "say nothing".
/// </summary>
public sealed record DictationNotice(string Message, NoticeLevel Level);

/// <summary>
/// Pure mapping from a <see cref="DictationOutcome"/> to the notice (if any) the
/// user should see. The single place that decides what each fail-soft outcome
/// says, so every caller surfaces them identically through an
/// <see cref="INotifier"/> instead of re-inventing balloon text.
/// </summary>
public static class DictationNotices
{
    /// <summary>
    /// The notice for <paramref name="outcome"/>, or null when the outcome is
    /// silent. <see cref="DictationOutcome.Injected"/> is the only silent case:
    /// a successful dictation needs no announcement.
    /// </summary>
    public static DictationNotice? For(DictationOutcome outcome) => outcome switch
    {
        DictationOutcome.Injected => null,
        DictationOutcome.NothingTranscribed =>
            new DictationNotice("(no speech detected)", NoticeLevel.Info),
        DictationOutcome.TranscriptionFailed =>
            new DictationNotice("Transcription failed.", NoticeLevel.Error),
        DictationOutcome.TranscribedOffline =>
            new DictationNotice("Cloud transcription offline — transcribed locally.", NoticeLevel.Warning),
        DictationOutcome.RefinedOffline =>
            new DictationNotice("Refinement offline — raw text inserted.", NoticeLevel.Warning),
        DictationOutcome.InjectionBlocked =>
            new DictationNotice("Couldn't paste — text left on clipboard.", NoticeLevel.Warning),
        _ => null,
    };
}
