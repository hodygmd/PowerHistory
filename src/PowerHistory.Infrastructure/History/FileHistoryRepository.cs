namespace PowerHistory.Infrastructure.History;

using System.Security.Cryptography;
using PowerHistory.Core.Interfaces;
using PowerHistory.Core.Models;

/// <summary>
/// Reads and writes history files on disk.
/// </summary>
/// <remarks>
/// Writes never touch the original in place: the result is written to a temporary file in the same
/// directory and then moved over the original, so a failure keeps the previous content intact.
/// </remarks>
public sealed class FileHistoryRepository : IHistoryFile
{
    public HistoryDocument Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var bytes = File.ReadAllBytes(path);
        var decoded = HistoryFileFormat.Decode(bytes);
        var entries = decoded.Lines.Select((text, index) => new HistoryEntry(index, text)).ToList();

        return new HistoryDocument(
            Path.GetFullPath(path),
            entries,
            ComputeHash(bytes),
            decoded.Format);
    }

    public void Write(HistoryDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var bytes = HistoryFileFormat.Encode(
            document.Entries.Select(entry => entry.Text).ToList(),
            document.Format);

        SafeFile.Write(document.SourcePath, bytes);
    }

    public bool HasChangedSince(string path, string contentHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            return true;
        }

        var current = ComputeHash(File.ReadAllBytes(path));
        return !string.Equals(current, contentHash, StringComparison.OrdinalIgnoreCase);
    }

    private static string ComputeHash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
