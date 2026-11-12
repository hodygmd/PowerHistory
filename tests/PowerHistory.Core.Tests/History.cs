namespace PowerHistory.Core.Tests;

using PowerHistory.Core.Models;

/// <summary>
/// Helpers to build history snapshots in the tests.
/// </summary>
internal static class History
{
    public static HistoryEntry[] Entries(params string[] texts) =>
        texts.Select((text, index) => new HistoryEntry(index, text)).ToArray();
}
