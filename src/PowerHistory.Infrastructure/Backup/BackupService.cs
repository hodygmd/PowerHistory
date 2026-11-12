namespace PowerHistory.Infrastructure.Backup;

using System.Security.Cryptography;
using PowerHistory.Core.Interfaces;
using PowerHistory.Core.Models;
using PowerHistory.Infrastructure.History;

/// <summary>
/// Stores copies of history files in a directory outside the history folder.
/// </summary>
public sealed class BackupService : IBackupService
{
    private readonly string _directory;

    public BackupService(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = Path.GetFullPath(directory);
    }

    /// <summary>Default backup directory under the user data folder.</summary>
    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PowerHistory",
        "backups");

    public BackupEntry Create(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        var fullPath = Path.GetFullPath(sourcePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"History file not found: {fullPath}", fullPath);
        }

        Directory.CreateDirectory(_directory);

        var destination = NextAvailablePath(BuildName(fullPath));
        SafeFile.Copy(fullPath, destination);

        var info = new FileInfo(destination);
        return new BackupEntry(
            Path.GetFileName(destination),
            destination,
            fullPath,
            new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero),
            info.Length);
    }

    public IReadOnlyList<BackupEntry> List(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        if (!Directory.Exists(_directory))
        {
            return [];
        }

        var prefix = BuildPrefix(Path.GetFullPath(sourcePath));
        var entries = new List<BackupEntry>();

        foreach (var file in Directory.EnumerateFiles(_directory, "*.bak"))
        {
            var name = Path.GetFileName(file);
            if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var info = new FileInfo(file);
            entries.Add(new BackupEntry(
                name,
                file,
                Path.GetFullPath(sourcePath),
                new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero),
                info.Length));
        }

        return entries.OrderByDescending(entry => entry.CreatedAt).ToList();
    }

    public void Restore(BackupEntry backup, string targetPath)
    {
        ArgumentNullException.ThrowIfNull(backup);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        if (!File.Exists(backup.Path))
        {
            throw new FileNotFoundException($"Backup file not found: {backup.Path}", backup.Path);
        }

        var target = Path.GetFullPath(targetPath);

        // The current state is backed up first, so a restore can always be undone.
        if (File.Exists(target))
        {
            Create(target);
        }

        SafeFile.Copy(backup.Path, target);
    }

    /// <summary>
    /// Backup file name: original name, a short key that identifies the source path,
    /// a timestamp and the extension.
    /// </summary>
    private static string BuildName(string sourcePath)
    {
        var baseName = Path.GetFileNameWithoutExtension(sourcePath);
        var key = PathKey(sourcePath);
        var stamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss-fff");
        return $"{baseName}.{key}.{stamp}.bak";
    }

    private static string BuildPrefix(string sourcePath)
    {
        var baseName = Path.GetFileNameWithoutExtension(sourcePath);
        return $"{baseName}.{PathKey(sourcePath)}.";
    }

    /// <summary>
    /// Short stable key for a path, so two different files with the same name do not share backups.
    /// </summary>
    private static string PathKey(string sourcePath)
    {
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(sourcePath));
        return Convert.ToHexString(hash, 0, 4).ToLowerInvariant();
    }

    private string NextAvailablePath(string name)
    {
        var candidate = Path.Combine(_directory, name);
        var counter = 1;

        while (File.Exists(candidate))
        {
            candidate = Path.Combine(
                _directory,
                $"{Path.GetFileNameWithoutExtension(name)}-{counter}{Path.GetExtension(name)}");
            counter++;
        }

        return candidate;
    }
}
