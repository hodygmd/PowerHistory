namespace PowerHistory.Core.Models;

/// <summary>
/// Encoding details of a history file, captured on read so a write can reproduce the original shape.
/// </summary>
/// <param name="NewLine">Line terminator used by the file ("\r\n", "\n" or "\r").</param>
/// <param name="HasBom">Whether the file starts with a byte order mark.</param>
/// <param name="EndsWithNewLine">Whether the last line is terminated.</param>
public sealed record HistoryFormat(string NewLine, bool HasBom, bool EndsWithNewLine)
{
    public static HistoryFormat Default { get; } = new("\n", false, true);
}
