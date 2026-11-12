namespace PowerHistory.Infrastructure.Tests;

/// <summary>
/// A disposable temporary directory used by the tests. The real history is never touched.
/// </summary>
internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "PowerHistoryTests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string Write(string name, string content, bool utf8Bom = false)
    {
        var full = System.IO.Path.Combine(Path, name);
        File.WriteAllText(full, content, new System.Text.UTF8Encoding(utf8Bom));
        return full;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                foreach (var file in Directory.EnumerateFiles(Path))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }

                Directory.Delete(Path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
