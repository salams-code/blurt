namespace Blurt.Core;

/// <summary>
/// Coordinates per-dictation cancellation (issue 48): hands each dictation its own
/// <see cref="CancellationTokenSource"/> via a <see cref="Handle"/> and remembers the
/// most-recent in-flight one so the Esc cancel affordance can abort it. Pure of any
/// Win32 — the keyboard hook calls <see cref="RequestCancel"/> on Esc and swallows the
/// key only when it returns <c>true</c> (a dictation was in flight to cancel);
/// otherwise Esc passes through to the focused app untouched.
///
/// Dictations are launched fire-and-forget and can overlap (a new one can start while
/// a slow one is still transcribing), so a single shared source would let the finishing
/// dictation tear down the newer one's. Each <see cref="Handle"/> therefore owns and
/// disposes only its own source, and ending one never disturbs another's. All state is
/// guarded by a lock because <see cref="RequestCancel"/> may run on the keyboard-hook
/// thread while <see cref="Begin"/>/Handle disposal run on the dictation flow.
/// </summary>
public sealed class DictationCancellation
{
    private readonly object _gate = new();

    // The most-recent in-flight dictation's source — the one Esc cancels. Each Handle
    // owns its own source; this only points at whichever began last and is still live.
    private CancellationTokenSource? _current;

    /// <summary>True while a dictation is in flight and not yet cancelled.</summary>
    public bool IsCancellable
    {
        get { lock (_gate) { return _current is { IsCancellationRequested: false }; } }
    }

    /// <summary>
    /// Begins a cancellable dictation. Dispose the returned <see cref="Handle"/> (via
    /// <c>using</c>) when the dictation ends; it releases only this dictation's source.
    /// </summary>
    public Handle Begin()
    {
        var cts = new CancellationTokenSource();
        lock (_gate) { _current = cts; }
        return new Handle(this, cts);
    }

    /// <summary>
    /// Requests cancellation of the in-flight dictation. Returns <c>true</c> only when
    /// there was a cancellable dictation, so the Esc handler swallows the key solely in
    /// that case and otherwise lets Esc reach the focused app.
    /// </summary>
    public bool RequestCancel()
    {
        lock (_gate)
        {
            if (_current is { IsCancellationRequested: false } cts)
            {
                cts.Cancel();
                return true;
            }

            return false;
        }
    }

    // Releases a dictation's source. Only clears _current when it still points at this
    // source, so a dictation finishing after a newer one started never clears the newer
    // one's cancellability.
    private void End(CancellationTokenSource cts)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_current, cts))
            {
                _current = null;
            }
        }

        cts.Dispose();
    }

    /// <summary>Scopes one dictation's cancellation; disposing it ends that dictation.</summary>
    public sealed class Handle : IDisposable
    {
        private readonly DictationCancellation _owner;
        private readonly CancellationTokenSource _cts;

        internal Handle(DictationCancellation owner, CancellationTokenSource cts)
        {
            _owner = owner;
            _cts = cts;
        }

        /// <summary>The token to hand to the pipeline for this dictation.</summary>
        public CancellationToken Token => _cts.Token;

        public void Dispose() => _owner.End(_cts);
    }
}
