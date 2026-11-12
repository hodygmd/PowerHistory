namespace PowerHistory.Cli.Tests;

/// <summary>
/// A disposable temporary directory used by the tests. The real history is never touched.
/// </summary>
internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "PowerHistoryCliTests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string Write(string name, string content)
    {
        var full = System.IO.Path.Combine(Path, name);
        File.WriteAllText(full, content);
        return full;
    }

    public string OptionsFile => System.IO.Path.Combine(Path, "config.json");

    public string BackupDirectory => System.IO.Path.Combine(Path, "backups");

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
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

/// <summary>
/// Captures everything the application writes to the console.
/// </summary>
internal sealed class ConsoleCapture : IDisposable
{
    private readonly TextWriter _originalOut = Console.Out;
    private readonly TextWriter _originalError = Console.Error;
    private readonly StringWriter _writer = new();

    public ConsoleCapture()
    {
        Console.SetOut(_writer);
        Console.SetError(_writer);
    }

    public string Text => _writer.ToString();

    public void Dispose()
    {
        Console.SetOut(_originalOut);
        Console.SetError(_originalError);
        _writer.Dispose();
    }
}
