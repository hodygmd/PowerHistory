namespace PowerHistory.Infrastructure.History;

/// <summary>
/// Writes files without ever leaving a partially written destination behind.
/// </summary>
/// <remarks>
/// The new content goes to a temporary file in the destination directory, is flushed to disk and
/// only then replaces the destination. A failure at any point keeps the original file intact.
/// </remarks>
internal static class SafeFile
{
    public static void Write(string destination, byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);

        WithTemporaryFile(destination, temporary =>
        {
            using var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.Write(content);
            stream.Flush(flushToDisk: true);
        });
    }

    public static void Copy(string sourceFile, string destination)
    {
        WithTemporaryFile(destination, temporary => File.Copy(sourceFile, temporary));
    }

    private static void WithTemporaryFile(string destination, Action<string> write)
    {
        var directory = Path.GetDirectoryName(destination)
            ?? throw new InvalidOperationException($"Cannot determine the directory of '{destination}'.");
        var fileName = Path.GetFileName(destination);
        var temporary = Path.Combine(directory, $".{fileName}.{Guid.NewGuid():N}.tmp");

        try
        {
            write(temporary);
            Replace(temporary, destination);
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }
    }

    /// <summary>
    /// Replaces the destination with the temporary file. <see cref="File.Replace(string, string, string?)"/>
    /// is atomic on NTFS and keeps the destination permissions; a plain move is used when the
    /// destination does not exist yet.
    /// </summary>
    private static void Replace(string temporary, string destination)
    {
        if (File.Exists(destination))
        {
            File.Replace(temporary, destination, null);
        }
        else
        {
            File.Move(temporary, destination);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // A leftover temporary file is harmless; the original is still intact.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
