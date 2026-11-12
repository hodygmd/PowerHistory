namespace PowerHistory.Core.Models;

/// <summary>
/// A history file found on the machine, together with the host that reported it.
/// </summary>
/// <param name="Host">Name of the PowerShell host.</param>
/// <param name="Path">Absolute path reported by <c>Get-PSReadLineOption</c>.</param>
/// <param name="SaveStyle">Value of <c>HistorySaveStyle</c>, when reported.</param>
/// <param name="MaximumHistoryCount">Value of <c>MaximumHistoryCount</c>, when reported.</param>
public sealed record DiscoveredHistory(string Host, string Path, string? SaveStyle, int? MaximumHistoryCount);
