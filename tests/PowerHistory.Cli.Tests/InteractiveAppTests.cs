namespace PowerHistory.Cli.Tests;

using Spectre.Console;
using Spectre.Console.Testing;
using PowerHistory.Cli.Interactive;

public class InteractiveAppTests
{
    [Fact]
    public void RendersTheHistoryEntries()
    {
        using var temp = new TempDirectory();
        var path = temp.Write("history.txt", "alpha\nbeta\n");
        var console = StartConsole();
        console.Input.PushCharacter('q');

        var exitCode = Run(temp, console, path);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("alpha", Rendered(console));
        Assert.Contains("beta", Rendered(console));
    }

    [Fact]
    public void FiltersTheEntriesWithTheSearchPrompt()
    {
        using var temp = new TempDirectory();
        var path = temp.Write("history.txt", "docker ps\ngit status\n");
        var console = StartConsole();
        console.Input.PushCharacter('/');
        console.Input.PushTextWithEnter("docker");
        console.Input.PushCharacter('q');

        Run(temp, console, path);

        var rendered = Rendered(console);
        Assert.Contains("docker ps", rendered);
        Assert.DoesNotContain("git status", rendered);
    }

    [Fact]
    public void DeletesTheSelectedEntryAfterTheConfirmation()
    {
        using var temp = new TempDirectory();
        var path = temp.Write("history.txt", "alpha\nbeta\n");
        var console = StartConsole();
        console.Input.PushKey(ConsoleKey.Spacebar);
        console.Input.PushCharacter('d');
        console.Input.PushTextWithEnter("y");
        console.Input.PushCharacter('q');

        Run(temp, console, path);

        Assert.Equal("beta\n", File.ReadAllText(path));
        Assert.Single(BackupFiles(temp));
    }

    [Fact]
    public void KeepsTheFileWhenTheDeletionIsCancelled()
    {
        using var temp = new TempDirectory();
        var path = temp.Write("history.txt", "alpha\nbeta\n");
        var console = StartConsole();
        console.Input.PushKey(ConsoleKey.Spacebar);
        console.Input.PushCharacter('d');
        console.Input.PushTextWithEnter("n");
        console.Input.PushCharacter('q');

        Run(temp, console, path);

        Assert.Equal("alpha\nbeta\n", File.ReadAllText(path));
        Assert.Empty(BackupFiles(temp));
    }

    [Fact]
    public void SelectsEveryVisibleEntryWithControlA()
    {
        using var temp = new TempDirectory();
        var path = temp.Write("history.txt", "alpha\nbeta\n");
        var console = StartConsole();
        console.Input.PushKey(new ConsoleKeyInfo('\x01', ConsoleKey.A, shift: false, alt: false, control: true));
        console.Input.PushCharacter('d');
        console.Input.PushTextWithEnter("y");
        console.Input.PushCharacter('q');

        Run(temp, console, path);

        Assert.Equal(string.Empty, File.ReadAllText(path));
    }

    private static TestConsole StartConsole()
    {
        var console = new TestConsole();
        AnsiConsole.Console = console;
        console.Width(120);
        return console;
    }

    /// <summary>
    /// The console keeps every render, so the assertions look at the last one only.
    /// Every render starts with a "Filter:" line.
    /// </summary>
    private static string Rendered(TestConsole console)
    {
        var lines = console.Lines.ToList();
        var lastFilter = lines.FindLastIndex(line => line.Contains("Filter:", StringComparison.Ordinal));

        return lastFilter < 0
            ? string.Join('\n', lines)
            : string.Join('\n', lines.Skip(lastFilter + 1));
    }

    private static string[] BackupFiles(TempDirectory temp) =>
        Directory.Exists(temp.BackupDirectory) ? Directory.GetFiles(temp.BackupDirectory, "*.bak") : [];

    private static int Run(TempDirectory temp, TestConsole console, string path)
    {
        var application = new CliApplication(temp.OptionsFile, temp.BackupDirectory);
        var interactive = new InteractiveApp(application, CommandLine.Parse(["--file", path]));
        return interactive.Run();
    }
}
