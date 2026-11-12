namespace PowerHistory.Core.Interfaces;

using PowerHistory.Core.Models;

/// <summary>
/// Locates the history files configured in the installed PowerShell hosts.
/// </summary>
public interface IHistoryDiscovery
{
    /// <summary>
    /// Queries every supported host. Hosts that are missing, unreadable or have no PSReadLine
    /// are skipped instead of failing the whole discovery.
    /// </summary>
    IReadOnlyList<DiscoveredHistory> Discover();
}
