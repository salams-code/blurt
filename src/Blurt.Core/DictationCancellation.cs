namespace Blurt.Core;

/// <summary>
/// Coordinates per-dictation cancellation (issue 48): owns the in-flight dictation's
/// <see cref="CancellationTokenSource"/> so the Esc cancel affordance can abort an
/// in-progress transcription/refinement. Pure of any Win32 — the keyboard hook calls
/// <see cref="RequestCancel"/> on Esc and swallows the key only when it returns
/// <c>true</c> (i.e. a dictation was actually in flight to cancel); otherwise Esc
/// passes through to the focused app untouched.
/// </summary>
public sealed class DictationCancellation
{
    private CancellationTokenSource? _cts;

    /// <summary>
    /// True while a dictation is in flight and not yet cancelled — i.e. there is
    /// something for Esc to abort. Goes false once cancelled or after <see cref="End"/>.
    /// </summary>
    public bool IsCancellable => _cts is { IsCancellationRequested: false };

    /// <summary>
    /// Begins a cancellable dictation and returns the token to hand to the pipeline.
    /// Replaces any prior source defensively (dictations are serial push-to-talk).
    /// </summary>
    public CancellationToken Begin()
    {
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        return _cts.Token;
    }

    /// <summary>
    /// Requests cancellation of the in-flight dictation. Returns <c>true</c> only when
    /// there was a cancellable dictation, so the Esc handler swallows the key solely in
    /// that case and otherwise lets Esc reach the focused app.
    /// </summary>
    public bool RequestCancel()
    {
        if (_cts is { IsCancellationRequested: false } cts)
        {
            cts.Cancel();
            return true;
        }

        return false;
    }

    /// <summary>Ends the current dictation and releases its source.</summary>
    public void End()
    {
        _cts?.Dispose();
        _cts = null;
    }
}
