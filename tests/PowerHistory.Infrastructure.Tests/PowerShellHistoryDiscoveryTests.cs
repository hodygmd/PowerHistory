namespace PowerHistory.Infrastructure.Tests;

using PowerHistory.Infrastructure.Discovery;

public class PowerShellHistoryDiscoveryTests
{
    [Fact]
    public void Discover_DoesNotThrowWhenNoHostIsAvailable()
    {
        var discovery = new PowerShellHistoryDiscovery();

        var discovered = discovery.Discover();

        Assert.NotNull(discovered);
    }

    [Fact]
    public void Discover_ReturnsAbsolutePathsAndNoDuplicates()
    {
        var discovery = new PowerShellHistoryDiscovery();

        var discovered = discovery.Discover();

        foreach (var entry in discovered)
        {
            Assert.True(Path.IsPathFullyQualified(entry.Path), $"'{entry.Path}' is not an absolute path.");
            Assert.False(string.IsNullOrWhiteSpace(entry.Host));
        }

        var paths = discovered.Select(entry => entry.Path).ToList();
        Assert.Equal(paths.Count, paths.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
