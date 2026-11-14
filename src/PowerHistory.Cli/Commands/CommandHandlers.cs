namespace PowerHistory.Cli.Commands;

using PowerHistory.Core.Models;
using PowerHistory.Core.Services;

/// <summary>
/// Non interactive commands. Every destructive command previews the change, requires an explicit
/// confirmation and creates a backup before writing.
/// </summary>
internal static class CommandHandlers
{
    public static int List(CliApplication app, CommandLine command)
    {
        var path = app.ResolveHistoryPath(command);
        if (path is null)
        {
            return ExitCodes.FileError;
        }

        var document = app.HistoryFile.Read(path);
        var limit = ReadLimit(command);
        var shown = 0;

        foreach (var entry in document.Entries)
        {
            if (limit > 0 && shown >= limit)
            {
                break;
            }

            Console.WriteLine($"{entry.Index}\t{entry.Text}");
            shown++;
        }

        Console.Error.WriteLine($"{shown} of {document.Entries.Count} entries from {path}");
        return ExitCodes.Success;
    }

    public static int Search(CliApplication app, CommandLine command)
    {
        var term = command.PositionalAt(0) ?? command.Value("text");
        if (string.IsNullOrWhiteSpace(term))
        {
            Console.Error.WriteLine("Usage: histo search <text>");
            return ExitCodes.UsageError;
        }

        var path = app.ResolveHistoryPath(command);
        if (path is null)
        {
            return ExitCodes.FileError;
        }

        var document = app.HistoryFile.Read(path);
        var matches = HistoryQuery.Find(document.Entries, term, IsCaseSensitive(command));
        var limit = ReadLimit(command);
        var shown = 0;

        foreach (var entry in matches)
        {
            if (limit > 0 && shown >= limit)
            {
                break;
            }

            Console.WriteLine($"{entry.Index}\t{entry.Text}");
            shown++;
        }

        Console.Error.WriteLine($"{matches.Count} matches for '{term}' in {path}");
        return ExitCodes.Success;
    }

    public static int Stats(CliApplication app, CommandLine command)
    {
        var path = app.ResolveHistoryPath(command);
        if (path is null)
        {
            return ExitCodes.FileError;
        }

        var document = app.HistoryFile.Read(path);
        var entries = document.Entries;
        var unique = new HashSet<string>(entries.Select(entry => entry.Text), StringComparer.Ordinal);
        var duplicates = entries.Count - unique.Count;
        var characters = entries.Sum(entry => entry.Text.Length);

        Console.WriteLine($"File:            {path}");
        Console.WriteLine($"Entries:         {entries.Count}");
        Console.WriteLine($"Unique commands: {unique.Count}");
        Console.WriteLine($"Duplicates:      {duplicates}");
        Console.WriteLine($"Characters:      {characters}");
        Console.WriteLine($"Size on disk:    {new FileInfo(path).Length} bytes");
        Console.WriteLine($"Newline:         {Escape(document.Format.NewLine)}");
        Console.WriteLine($"Hash (SHA-256):  {document.ContentHash}");
        return ExitCodes.Success;
    }

    public static int Backup(CliApplication app, CommandLine command)
    {
        var path = app.ResolveHistoryPath(command);
        if (path is null)
        {
            return ExitCodes.FileError;
        }

        app.ConfigureBackups();
        var backup = app.Backups.Create(path);
        Console.WriteLine($"Backup created: {backup.Path}");
        Console.WriteLine($"Size: {backup.SizeBytes} bytes");
        return ExitCodes.Success;
    }

    public static int Backups(CliApplication app, CommandLine command)
    {
        var path = app.ResolveHistoryPath(command);
        if (path is null)
        {
            return ExitCodes.FileError;
        }

        app.ConfigureBackups();
        var backups = app.Backups.List(path);

        if (backups.Count == 0)
        {
            Console.WriteLine("No backups found.");
            return ExitCodes.Success;
        }

        foreach (var backup in backups)
        {
            Console.WriteLine($"{backup.Id}\t{backup.CreatedAt:yyyy-MM-dd HH:mm:ss}\t{backup.SizeBytes} bytes");
        }

        return ExitCodes.Success;
    }

    public static int Restore(CliApplication app, CommandLine command)
    {
        var id = command.PositionalAt(0) ?? command.Value("id");
        if (string.IsNullOrWhiteSpace(id))
        {
            Console.Error.WriteLine("Usage: histo restore <backup-id>");
            return ExitCodes.UsageError;
        }

        var path = app.ResolveHistoryPath(command);
        if (path is null)
        {
            return ExitCodes.FileError;
        }

        app.ConfigureBackups();
        var backup = app.Backups.List(path).FirstOrDefault(entry =>
            entry.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

        if (backup is null)
        {
            Console.Error.WriteLine($"Backup '{id}' not found for {path}.");
            return ExitCodes.FileError;
        }

        if (!command.Flag("yes"))
        {
            Console.WriteLine($"Restore '{backup.Id}' over {path}? ({backup.SizeBytes} bytes)");
            Console.Error.WriteLine("Run again with --yes to confirm.");
            return ExitCodes.Aborted;
        }

        app.Backups.Restore(backup, path);
        Console.WriteLine($"Restored '{backup.Id}' into {path}.");
        Console.WriteLine("The previous content was backed up before the restore.");
        return ExitCodes.Success;
    }

    public static int Clean(CliApplication app, CommandLine command)
    {
        var path = app.ResolveHistoryPath(command);
        if (path is null)
        {
            return ExitCodes.FileError;
        }

        var document = app.HistoryFile.Read(path);
        var removals = ComputeRemovals(document.Entries, command);

        if (removals.Count == 0)
        {
            Console.Error.WriteLine("No filter selected. Use --duplicates, --text, --regex, --empty or --keep-last.");
            return ExitCodes.UsageError;
        }

        var remaining = HistoryCleaner.Remove(document.Entries, removals);

        Console.WriteLine($"{removals.Count} entries would be removed from {path} ({remaining.Count} would remain).");
        foreach (var entry in document.Entries.Where(entry => removals.Contains(entry.Index)).Take(20))
        {
            Console.WriteLine($"  - {entry.Text}");
        }

        if (removals.Count > 20)
        {
            Console.WriteLine($"  ... and {removals.Count - 20} more");
        }

        if (!command.Flag("yes"))
        {
            Console.Error.WriteLine("Run again with --yes to apply the change.");
            return ExitCodes.Aborted;
        }

        return ApplyChanges(app, document, remaining);
    }

    /// <summary>
    /// Verifies that the file did not change, creates a backup and writes the new content.
    /// </summary>
    internal static int ApplyChanges(CliApplication app, HistoryDocument document, IReadOnlyList<HistoryEntry> remaining)
    {
        if (app.HistoryFile.HasChangedSince(document.SourcePath, document.ContentHash))
        {
            Console.Error.WriteLine($"The file changed since it was read: {document.SourcePath}");
            Console.Error.WriteLine("Nothing was written. Reload the file and review the change again.");
            return ExitCodes.ExternalChange;
        }

        app.ConfigureBackups();
        var backup = app.Backups.Create(document.SourcePath);
        Console.WriteLine($"Backup created: {backup.Path}");

        var updated = document with { Entries = remaining };
        app.HistoryFile.Write(updated);

        var verified = app.HistoryFile.Read(document.SourcePath);
        Console.WriteLine($"{document.Entries.Count - remaining.Count} entries removed. {verified.Entries.Count} entries remain.");
        return ExitCodes.Success;
    }

    /// <summary>
    /// Builds the set of entry indices selected by the clean filters.
    /// </summary>
    /// <remarks>
    /// The filters compose with AND: an entry is removed only when it satisfies every filter that
    /// was passed. That keeps <c>--duplicates --text node</c> from deleting duplicates that do not
    /// contain <c>node</c>.
    /// </remarks>
    private static HashSet<int> ComputeRemovals(IReadOnlyList<HistoryEntry> entries, CommandLine command)
    {
        var predicates = new List<Func<HistoryEntry, bool>>();
        var caseSensitive = IsCaseSensitive(command);
        var exact = command.Flag("exact");

        if (command.Flag("duplicates"))
        {
            var keepFirst = !command.Flag("keep-last-occurrence");
            var keep = HistoryCleaner.RemoveDuplicates(entries, keepFirst)
                .Select(entry => entry.Index)
                .ToHashSet();

            predicates.Add(entry => !keep.Contains(entry.Index));
        }

        if (command.Value("text") is { Length: > 0 } text)
        {
            predicates.Add(entry => HistoryQuery.Matches(entry.Text, text, caseSensitive, exact));
        }

        if (command.Value("regex") is { Length: > 0 } pattern)
        {
            var regex = new System.Text.RegularExpressions.Regex(pattern);
            predicates.Add(entry => regex.IsMatch(entry.Text));
        }

        if (command.Flag("empty"))
        {
            predicates.Add(entry => string.IsNullOrWhiteSpace(entry.Text));
        }

        if (command.Value("keep-last") is { } keepLastText
            && int.TryParse(keepLastText, out var keepLast)
            && keepLast >= 0)
        {
            var firstKept = entries.Count - keepLast;
            predicates.Add(entry => entry.Index < firstKept);
        }

        if (predicates.Count == 0)
        {
            return [];
        }

        return entries
            .Where(entry => predicates.All(predicate => predicate(entry)))
            .Select(entry => entry.Index)
            .ToHashSet();
    }

    private static bool IsCaseSensitive(CommandLine command) =>
        command.Flag("case-sensitive") || command.Flag("case-sensitive-search");

    private static int ReadLimit(CommandLine command)
    {
        if (command.Value("limit") is { } text && int.TryParse(text, out var limit) && limit > 0)
        {
            return limit;
        }

        return 0;
    }

    private static string Escape(string value) => value.Replace("\r", "\\r").Replace("\n", "\\n");
}
