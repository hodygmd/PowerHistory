namespace PowerHistory.Core.Services;

using PowerHistory.Core.Models;

/// <summary>
/// Text search over history entries.
/// </summary>
public static class HistoryQuery
{
    /// <summary>
    /// Returns the entries containing <paramref name="query"/>, in file order.
    /// An empty query matches every entry.
    /// </summary>
    public static IReadOnlyList<HistoryEntry> Find(
        IReadOnlyList<HistoryEntry> entries,
        string query,
        bool caseSensitive = false)
    {
        ArgumentNullException.ThrowIfNull(entries);

        if (string.IsNullOrEmpty(query))
        {
            return entries;
        }

        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return entries.Where(entry => entry.Text.Contains(query, comparison)).ToList();
    }

    /// <summary>
    /// Returns true when <paramref name="text"/> contains <paramref name="query"/>.
    /// </summary>
    public static bool Matches(string text, string query, bool caseSensitive = false)
    {
        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return text.Contains(query, comparison);
    }
}
