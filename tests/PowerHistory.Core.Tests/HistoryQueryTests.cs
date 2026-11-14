namespace PowerHistory.Core.Tests;

using PowerHistory.Core.Services;

public class HistoryQueryTests
{
    [Fact]
    public void Find_IsCaseInsensitiveByDefault()
    {
        var entries = History.Entries("Get-Process", "GET-PROCESS", "Get-Service");

        var matches = HistoryQuery.Find(entries, "get-process");

        Assert.Equal(2, matches.Count);
    }

    [Fact]
    public void Find_RespectsCaseSensitivity()
    {
        var entries = History.Entries("Get-Process", "GET-PROCESS");

        var matches = HistoryQuery.Find(entries, "GET-PROCESS", caseSensitive: true);

        Assert.Single(matches);
        Assert.Equal("GET-PROCESS", matches[0].Text);
    }

    [Fact]
    public void Find_ReturnsEntriesInFileOrder()
    {
        var entries = History.Entries("alpha", "beta", "alpha", "gamma");

        var matches = HistoryQuery.Find(entries, "alpha");

        Assert.Equal(new[] { 0, 2 }, matches.Select(entry => entry.Index));;
    }

    [Fact]
    public void Find_WithEmptyQueryReturnsEveryEntry()
    {
        var entries = History.Entries("a", "b");

        var matches = HistoryQuery.Find(entries, string.Empty);

        Assert.Equal(2, matches.Count);
    }

    [Fact]
    public void Find_WithNoMatchReturnsNothing()
    {
        var entries = History.Entries("a", "b");

        Assert.Empty(HistoryQuery.Find(entries, "zzz"));
    }

    [Theory]
    [InlineData("docker ps", "docker", false, true)]
    [InlineData("docker ps", "DOCKER", false, true)]
    [InlineData("docker ps", "DOCKER", true, false)]
    [InlineData("docker ps", "podman", false, false)]
    public void Matches_BehavesAsExpected(string text, string query, bool caseSensitive, bool expected)
    {
        Assert.Equal(expected, HistoryQuery.Matches(text, query, caseSensitive));
    }

    [Fact]
    public void Find_WithExactMatchIgnoresLongerCommands()
    {
        var entries = History.Entries("node", "node --run dev", "npm", "NODE");

        var matches = HistoryQuery.Find(entries, "node", exact: true);

        Assert.Equal(new[] { 0, 3 }, matches.Select(entry => entry.Index));
    }

    [Fact]
    public void Find_WithExactMatchRespectsCaseSensitivity()
    {
        var entries = History.Entries("node", "NODE");

        var matches = HistoryQuery.Find(entries, "node", caseSensitive: true, exact: true);

        Assert.Single(matches);
        Assert.Equal("node", matches[0].Text);
    }

    [Theory]
    [InlineData("node", "node", false, true)]
    [InlineData("node --run dev", "node", false, false)]
    [InlineData("NODE", "node", false, true)]
    [InlineData("NODE", "node", true, false)]
    public void Matches_WithExactMatchComparesTheWholeText(
        string text,
        string query,
        bool caseSensitive,
        bool expected)
    {
        Assert.Equal(expected, HistoryQuery.Matches(text, query, caseSensitive, exact: true));
    }
}
