namespace PowerHistory.Core.Interfaces;

using PowerHistory.Core.Models;

/// <summary>
/// Read and write access to a history file.
/// </summary>
public interface IHistoryFile
{
    /// <summary>Reads the file and returns an immutable snapshot.</summary>
    HistoryDocument Read(string path);

    /// <summary>Writes the document back to <see cref="HistoryDocument.SourcePath"/> using a safe replace.</summary>
    void Write(HistoryDocument document);

    /// <summary>
    /// Returns true when the file on disk no longer matches <paramref name="contentHash"/>.
    /// A missing file counts as changed.
    /// </summary>
    bool HasChangedSince(string path, string contentHash);
}
