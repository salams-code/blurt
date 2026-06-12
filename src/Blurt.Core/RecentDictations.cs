using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Blurt.Core;

/// <summary>
/// The last few dictation results, newest first (issue 26) — so a paste that
/// landed in the void (cursor moved, no field focused) can be recovered from
/// the tray menu instead of re-dictating.
///
/// Privacy contract: in-memory only. Entries are never written to disk and die
/// with the process; a fresh instance (every launch) starts empty.
/// </summary>
public sealed class RecentDictations
{
    private readonly List<string> _items = [];

    /// <summary>The retained results, newest first. Empty until the first add.</summary>
    public IReadOnlyList<string> Items => _items;

    /// <summary>How many results are retained; the oldest is evicted beyond this.</summary>
    public const int Capacity = 3;

    /// <summary>Record a dictation result as the newest entry, evicting the
    /// oldest once <see cref="Capacity"/> is reached.</summary>
    public void Add(string text)
    {
        // A blank result (silence, mic glitch) has nothing to recover — adding it
        // would only evict a real entry.
        if (string.IsNullOrWhiteSpace(text))
            return;

        _items.Insert(0, text);
        if (_items.Count > Capacity)
            _items.RemoveAt(_items.Count - 1);
    }

    /// <summary>
    /// One readable menu-label line for a result: newlines and whitespace runs
    /// collapse to single spaces, and anything longer than 48 characters is cut
    /// with a visible ellipsis (Bullets output is multi-line; a menu item isn't).
    /// </summary>
    public static string Preview(string text)
    {
        var oneLine = Regex.Replace(text, @"\s+", " ").Trim();
        const int max = 48;
        return oneLine.Length <= max ? oneLine : oneLine[..(max - 1)] + "…";
    }
}
