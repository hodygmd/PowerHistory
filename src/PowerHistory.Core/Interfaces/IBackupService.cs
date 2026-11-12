namespace PowerHistory.Core.Interfaces;

using PowerHistory.Core.Models;

/// <summary>
/// Creates, lists and restores backups of history files.
/// </summary>
public interface IBackupService
{
    /// <summary>Copies <paramref name="sourcePath"/> into the backup directory and returns the new backup.</summary>
    BackupEntry Create(string sourcePath);

    /// <summary>Lists the backups taken from <paramref name="sourcePath"/>, newest first.</summary>
    IReadOnlyList<BackupEntry> List(string sourcePath);

    /// <summary>
    /// Restores a backup over <paramref name="targetPath"/>. The current state of the target is
    /// backed up first, so a restore is always reversible.
    /// </summary>
    void Restore(BackupEntry backup, string targetPath);
}
