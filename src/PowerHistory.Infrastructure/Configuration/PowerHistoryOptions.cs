namespace PowerHistory.Infrastructure.Configuration;

/// <summary>
/// User settings, stored as JSON outside the history folder.
/// </summary>
public sealed class PowerHistoryOptions
{
    /// <summary>History file used when the user does not pick one.</summary>
    public string? DefaultHistoryPath { get; set; }

    /// <summary>Directory where backups are written.</summary>
    public string? BackupDirectory { get; set; }

    /// <summary>
    /// Maximum number of entries shown at once in the interactive list.
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>Require an explicit confirmation before a destructive operation.</summary>
    public bool RequireConfirmation { get; set; } = true;

    /// <summary>Search is case insensitive unless this is enabled.</summary>
    public bool CaseSensitiveSearch { get; set; }
}
