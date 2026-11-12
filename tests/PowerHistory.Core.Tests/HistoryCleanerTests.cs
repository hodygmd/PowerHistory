namespace PowerHistory.Core.Tests;

using PowerHistory.Core.Services;

public class HistoryCleanerTests
{
    [Fact]
    public void Remove_DropsTheSelectedSnapshotIndices()
    {
        var entries = History.Entries("a", "b", "c", "d");

        var result = HistoryCleaner.Remove(entries, [1, 3]);

        Assert.Equal(new[] { "a", "c" }, result.Select(entry => entry.Text));
        Assert.Equal(new[] { 0, 2 }, result.Select(entry => entry.Index));
    }

    [Fact]
    public void Remove_OperatesOnTheSnapshotAndNotOnRecalculatedIndices()
    {
        // The indices refer to the original snapshot: removing "b" (index 1) must not shift "c" (index 2).
        var entries = History.Entries("a", "b", "c");

        var result = HistoryCleaner.Remove(entries, [1]);

        Assert.Equal(new[] { "a", "c" }, result.Select(entry => entry.Text));
    }

    [Fact]
    public void RemoveDuplicates_KeepsFirstOccurrenceByDefault()
    {
        var entries = History.Entries("a", "b", "a", "c", "b");

        var result = HistoryCleaner.RemoveDuplicates(entries);

        Assert.Equal(new[] { "a", "b", "c" }, result.Select(entry => entry.Text));
    }

    [Fact]
    public void RemoveDuplicates_KeepsLastOccurrenceWhenAsked()
    {
        var entries = History.Entries("a", "b", "a", "c", "b");

        var result = HistoryCleaner.RemoveDuplicates(entries, keepFirst: false);

        Assert.Equal(new[] { "a", "c", "b" }, result.Select(entry => entry.Text));
    }

    [Fact]
    public void RemoveDuplicates_IsExactAndKeepsDifferentWhitespace()
    {
        var entries = History.Entries("ls -la", "ls -la ", "ls  -la");

        var result = HistoryCleaner.RemoveDuplicates(entries);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void RemoveMatching_RemovesEntriesContainingTheText()
    {
        var entries = History.Entries("docker ps", "git status", "docker compose up");

        var result = HistoryCleaner.RemoveMatching(entries, "docker");

        Assert.Equal(new[] { "git status" }, result.Select(entry => entry.Text));
    }

    [Fact]
    public void RemoveMatching_IsCaseInsensitiveByDefault()
    {
        var entries = History.Entries("Docker ps", "git status");

        var result = HistoryCleaner.RemoveMatching(entries, "docker");

        Assert.Equal(new[] { "git status" }, result.Select(entry => entry.Text));
    }

    [Fact]
    public void RemoveByRegex_RemovesMatchingEntries()
    {
        var entries = History.Entries("rm -rf /tmp/x", "ls", "Get-ChildItem C:\\temp");

        var result = HistoryCleaner.RemoveByRegex(entries, @"^rm\s");

        Assert.Equal(new[] { "ls", "Get-ChildItem C:\\temp" }, result.Select(entry => entry.Text));
    }

    [Fact]
    public void RemoveByRegex_ThrowsOnInvalidPattern()
    {
        var entries = History.Entries("a");

        Assert.ThrowsAny<ArgumentException>(() => HistoryCleaner.RemoveByRegex(entries, "[unclosed"));
    }

    [Fact]
    public void RemoveEmpty_RemovesBlankEntries()
    {
        var entries = History.Entries("a", string.Empty, "   ", "b");

        var result = HistoryCleaner.RemoveEmpty(entries);

        Assert.Equal(new[] { "a", "b" }, result.Select(entry => entry.Text));
    }

    [Fact]
    public void KeepLast_KeepsTheLastEntries()
    {
        var entries = History.Entries("a", "b", "c", "d");

        var result = HistoryCleaner.KeepLast(entries, 2);

        Assert.Equal(new[] { "c", "d" }, result.Select(entry => entry.Text));
    }

    [Fact]
    public void KeepLast_WhenCountIsNotSmallerThanTheSnapshotKeepsEverything()
    {
        var entries = History.Entries("a", "b");

        var result = HistoryCleaner.KeepLast(entries, 10);

        Assert.Equal(2, result.Count);
    }
}
