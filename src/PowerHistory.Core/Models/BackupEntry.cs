namespace PowerHistory.Core.Models;

/// <summary>
/// A backup copy of a history file.
/// </summary>
/// <param name="Id">File name of the backup inside the backup directory.</param>
/// <param name="Path">Absolute path of the backup file.</param>
/// <param name="SourcePath">Absolute path of the history file it was taken from.</param>
/// <param name="CreatedAt">Creation timestamp of the backup file.</param>
/// <param name="SizeBytes">Size of the backup file in bytes.</param>
public sealed record BackupEntry(string Id, string Path, string SourcePath, DateTimeOffset CreatedAt, long SizeBytes);
