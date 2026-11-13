namespace PowerHistory.Cli.Tests;

public class CliCommandTests
{
    [Fact]
    public void List_PrintsEveryEntry()
    {
        using var temp = new TempDirectory();
        var path = temp.Write("history.txt", "alpha\nbeta\n");

        var (exitCode, output) = Run(temp, "list", "--file", path);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("alpha", output);
        Assert.Contains("beta", output);
    }

    [Fact]
    public void Search_PrintsOnlyTheMatches()
    {
        using var temp = new TempDirectory();
        var path = temp.Write("history.txt", "docker ps\ngit status\ndocker compose\n");

        var (exitCode, output) = Run(temp, "search", "docker", "--file", path);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("docker ps", output);
        Assert.Contains("docker compose", output);
        Assert.DoesNotContain("git status", output);
    }

    [Fact]
    public void Stats_ReportsTheEntryCount()
    {
        using var temp = new TempDirectory();
        var path = temp.Write("history.txt", "alpha\nbeta\nalpha\n");

        var (exitCode, output) = Run(temp, "stats", "--file", path);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("Entries:         3", output);
        Assert.Contains("Unique commands: 2", output);
    }

    [Fact]
    public void Clean_WithoutConfirmationAbortsAndKeepsTheFile()
    {
        using var temp = new TempDirectory();
        var path = temp.Write("history.txt", "alpha\nbeta\nalpha\n");
        var original = File.ReadAllText(path);

        var (exitCode, output) = Run(temp, "clean", "--duplicates", "--file", path);

        Assert.Equal(ExitCodes.Aborted, exitCode);
        Assert.Contains("would be removed", output);
        Assert.Equal(original, File.ReadAllText(path));
    }

    [Fact]
    public void Clean_WithConfirmationRemovesTheEntriesAndKeepsABackup()
    {
        using var temp = new TempDirectory();
        var path = temp.Write("history.txt", "alpha\nbeta\nalpha\n");

        var (exitCode, output) = Run(temp, "clean", "--duplicates", "--yes", "--file", path);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal("alpha\nbeta\n", File.ReadAllText(path));
        Assert.Contains("Backup created", output);
        Assert.Single(Directory.GetFiles(temp.BackupDirectory, "*.bak"));
    }

    [Fact]
    public void Clean_WithoutAnyFilterIsAUsageError()
    {
        using var temp = new TempDirectory();
        var path = temp.Write("history.txt", "alpha\n");

        var (exitCode, _) = Run(temp, "clean", "--yes", "--file", path);

        Assert.Equal(ExitCodes.UsageError, exitCode);
    }

    [Fact]
    public void Clean_ByTextRemovesTheMatchingEntries()
    {
        using var temp = new TempDirectory();
        var path = temp.Write("history.txt", "docker ps\ngit status\ndocker compose\n");

        var (exitCode, _) = Run(temp, "clean", "--text", "docker", "--yes", "--file", path);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal("git status\n", File.ReadAllText(path));
    }

    [Fact]
    public void Clean_ByRegexRemovesTheMatchingEntries()
    {
        using var temp = new TempDirectory();
        var path = temp.Write("history.txt", "rm -rf /tmp/x\nls\n");

        var (exitCode, _) = Run(temp, "clean", "--regex", "^rm\\s", "--yes", "--file", path);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal("ls\n", File.ReadAllText(path));
    }

    [Fact]
    public void Clean_KeepsTheLastEntries()
    {
        using var temp = new TempDirectory();
        var path = temp.Write("history.txt", "a\nb\nc\nd\n");

        var (exitCode, _) = Run(temp, "clean", "--keep-last", "2", "--yes", "--file", path);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal("c\nd\n", File.ReadAllText(path));
    }

    [Fact]
    public void Restore_BringsTheOriginalContentBack()
    {
        using var temp = new TempDirectory();
        var path = temp.Write("history.txt", "alpha\nbeta\nalpha\n");

        Run(temp, "clean", "--duplicates", "--yes", "--file", path);
        Assert.Equal("alpha\nbeta\n", File.ReadAllText(path));

        var (_, backups) = Run(temp, "backups", "--file", path);
        var backupId = backups
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .First(line => line.Contains(".bak"))
            .Split('\t')[0];

        var (exitCode, _) = Run(temp, "restore", backupId, "--yes", "--file", path);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal("alpha\nbeta\nalpha\n", File.ReadAllText(path));
    }

    [Fact]
    public void Help_IsPrintedWithoutTouchingAnyFile()
    {
        var (exitCode, output) = Run(new TempDirectory(), "--help");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("PowerHistory", output);
        Assert.Contains("histo", output);
    }

    [Fact]
    public void Version_IsPrinted()
    {
        var (exitCode, output) = Run(new TempDirectory(), "--version");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Matches(@"^\d+\.\d+\.\d+", output.Trim());
    }

    [Fact]
    public void UnknownCommand_IsAUsageError()
    {
        var (exitCode, _) = Run(new TempDirectory(), "nonsense");

        Assert.Equal(ExitCodes.UsageError, exitCode);
    }

    [Fact]
    public void MissingFile_IsReported()
    {
        using var temp = new TempDirectory();
        var missing = Path.Combine(temp.Path, "missing.txt");

        var (exitCode, output) = Run(temp, "list", "--file", missing);

        Assert.Equal(ExitCodes.FileError, exitCode);
        Assert.Contains("not found", output);
    }

    private static (int ExitCode, string Output) Run(TempDirectory temp, params string[] args)
    {
        var application = new CliApplication(temp.OptionsFile, temp.BackupDirectory);

        using var capture = new ConsoleCapture();
        var exitCode = application.Run(args);
        return (exitCode, capture.Text);
    }
}
