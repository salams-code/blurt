namespace Blurt.Core;

/// <summary>
/// Lazily creates an expensive async value (e.g. a provisioned transcriber)
/// exactly once and hands the same instance to every caller — but unlike a
/// plain cached task, a failed attempt is forgotten so the next request tries
/// again. A transient failure (network, missing device) must not require an
/// app restart to recover from.
/// </summary>
public sealed class AsyncLazy<T>
{
    private readonly Func<Task<T>> _factory;
    private Task<T>? _value;

    public AsyncLazy(Func<Task<T>> factory) => _factory = factory;

    /// <summary>
    /// The cached value task; a still-running attempt is shared (no duplicate
    /// provisioning), a failed or canceled one is replaced by a fresh attempt.
    /// </summary>
    public Task<T> GetAsync()
    {
        if (_value is null || _value.IsFaulted || _value.IsCanceled)
        {
            _value = _factory();
        }

        return _value;
    }
}
