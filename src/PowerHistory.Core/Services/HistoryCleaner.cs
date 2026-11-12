namespace PowerHistory.Core.Services;

using PowerHistory.Core.Models;

/// <summary>
/// Pure transformations over a history snapshot. Every operation returns the entries to keep,
/// so the caller can preview the result before writing anything.
/// </summary>
public static class HistoryCleaner
{
    /// <summary>
    /// Removes the entries at the given indices. The indices refer to the snapshot that was read,
    /// never to a list recomputed after a previous removal.
    /// </summary>
    public static IReadOnlyList<HistoryEntry> Remove(IReadOnlyList<HistoryEntry> entries, IEnumerable<int> indices)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(indices);

        var removed = indices.ToHashSet();
        return entries.Where(entry => !removed.Contains(entry.Index)).ToList();
    }

    /// <summary>
    /// Removes exact duplicate texts.
    /// </summary>
    /// <param name="keepFirst">
    /// When true only the first occurrence of each text is kept; when false only the last one is.
    /// </param>
    public static IReadOnlyList<HistoryEntry> RemoveDuplicates(IReadOnlyList<HistoryEntry> entries, bool keepFirst = true)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var keep = new HashSet<int>();

        if (keepFirst)
        {
            foreach (var entry in entries)
            {
                if (seen.Add(entry.Text))
                {
                    keep.Add(entry.Index);
                }
            }
        }
        else
        {
            for (var i = entries.Count - 1; i >= 0; i--)
            {
                if (seen.Add(entries[i].Text))
                {
                    keep.Add(entries[i].Index);
                }
            }
        }

        return entries.Where(entry => keep.Contains(entry.Index)).ToList();
    }

    /// <summary>Removes every entry containing <paramref name="text"/>.</summary>
    public static IReadOnlyList<HistoryEntry> RemoveMatching(
        IReadOnlyList<HistoryEntry> entries,
        string text,
        bool caseSensitive = false)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return entries.Where(entry => !HistoryQuery.Matches(entry.Text, text, caseSensitive)).ToList();
    }

    /// <summary>Removes every entry matching <paramref name="pattern"/>.</summary>
    /// <exception cref="ArgumentException">The pattern is not a valid regular expression.</exception>
    public static IReadOnlyList<HistoryEntry> RemoveByRegex(IReadOnlyList<HistoryEntry> entries, string pattern)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(pattern);

        var regex = new System.Text.RegularExpressions.Regex(pattern);
        return entries.Where(entry => !regex.IsMatch(entry.Text)).ToList();
    }

    /// <summary>Removes entries whose text is empty or only whitespace.</summary>
    public static IReadOnlyList<HistoryEntry> RemoveEmpty(IReadOnlyList<HistoryEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return entries.Where(entry => !string.IsNullOrWhiteSpace(entry.Text)).ToList();
    }

    /// <summary>Keeps only the last <paramref name="count"/> entries.</summary>
    public static IReadOnlyList<HistoryEntry> KeepLast(IReadOnlyList<HistoryEntry> entries, int count)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (count >= entries.Count)
        {
            return entries;
        }

        return entries.Skip(entries.Count - count).ToList();
    }
}
