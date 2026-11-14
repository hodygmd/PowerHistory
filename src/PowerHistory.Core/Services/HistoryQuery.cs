namespace PowerHistory.Core.Services;

using PowerHistory.Core.Models;

/// <summary>
/// Text search over history entries.
/// </summary>
public static class HistoryQuery
{
    /// <summary>
    /// Returns the entries matching <paramref name="query"/>, in file order.
    /// An empty query matches every entry.
    /// </summary>
    /// <param name="exact">
    /// When true the whole entry must equal the query, so <c>node</c> does not match
    /// <c>node --run dev</c>; when false the entry only has to contain it.
    /// </param>
    public static IReadOnlyList<HistoryEntry> Find(
        IReadOnlyList<HistoryEntry> entries,
        string query,
        bool caseSensitive = false,
        bool exact = false)
    {
        ArgumentNullException.ThrowIfNull(entries);

        if (string.IsNullOrEmpty(query))
        {
            return entries;
        }

        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        return exact
            ? entries.Where(entry => string.Equals(entry.Text, query, comparison)).ToList()
            : entries.Where(entry => entry.Text.Contains(query, comparison)).ToList();
    }

    /// <summary>
    /// Returns true when <paramref name="text"/> matches <paramref name="query"/>,
    /// either containing it or, with <paramref name="exact"/>, being equal to it.
    /// </summary>
    public static bool Matches(string text, string query, bool caseSensitive = false, bool exact = false)
    {
        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        return exact
            ? string.Equals(text, query, comparison)
            : text.Contains(query, comparison);
    }
}
