namespace PowerHistory.Core.Models;

/// <summary>
/// An immutable snapshot of a history file.
/// </summary>
/// <param name="SourcePath">Absolute path of the file the snapshot was read from.</param>
/// <param name="Entries">Entries in file order. <see cref="HistoryEntry.Index"/> matches the position in this list.</param>
/// <param name="ContentHash">Fingerprint of the raw bytes, used to detect external modifications.</param>
/// <param name="Format">Encoding details needed to write the file back in its original shape.</param>
public sealed record HistoryDocument(
    string SourcePath,
    IReadOnlyList<HistoryEntry> Entries,
    string ContentHash,
    HistoryFormat Format);
