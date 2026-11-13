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
        Assert.Contains("alpha", Rendered(console, path));
        Assert.Contains("beta", Rendered(console, path));
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

        var rendered = Rendered(console, path);
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

    [Fact]
    public void FiltersWhileTypingWithoutPressingEnter()
    {
        using var temp = new TempDirectory();
        var path = temp.Write("history.txt", "docker ps\ngit status\n");
        var console = StartConsole();
        console.Input.PushCharacter('/');
        console.Input.PushText("docker");
        console.Input.PushKey(ConsoleKey.Escape);
        console.Input.PushCharacter('q');

        Run(temp, console, path);

        Assert.Contains("1 shown", console.Output);
    }

    [Fact]
    public void EscapeRestoresTheFilterThatWasActiveBeforeSearching()
    {
        using var temp = new TempDirectory();
        var path = temp.Write("history.txt", "docker ps\ngit status\n");
        var console = StartConsole();
        console.Input.PushCharacter('/');
        console.Input.PushTextWithEnter("docker");
        console.Input.PushCharacter('/');
        console.Input.PushText("xyz");
        console.Input.PushKey(ConsoleKey.Escape);
        console.Input.PushCharacter('q');

        Run(temp, console, path);

        Assert.Contains("filter: docker", Rendered(console, path));
        Assert.DoesNotContain("filter: xyz", Rendered(console, path));
    }

    [Fact]
    public void HidesTheCommitHashFromTheHeader()
    {
        using var temp = new TempDirectory();
        var path = temp.Write("history.txt", "alpha\n");
        var console = StartConsole();
        console.Input.PushCharacter('q');

        Run(temp, console, path);

        Assert.Contains("PowerHistory", console.Output);
        Assert.DoesNotContain("0.1.0+", console.Output);
    }

    [Theory]
    [InlineData(24)]
    [InlineData(30)]
    [InlineData(50)]
    public void TheConfirmationAndTheStatusFitTheTerminal(int height)
    {
        using var temp = new TempDirectory();
        var entries = string.Join("\n", Enumerable.Range(0, 200).Select(i => $"command {i}"));
        var path = temp.Write("history.txt", entries);
        var console = new TestConsole();
        AnsiConsole.Console = console;
        console.Width(90);
        console.Height(height);
        console.Input.PushKey(ConsoleKey.Spacebar);
        console.Input.PushCharacter('d');
        console.Input.PushCharacter('y');
        console.Input.PushCharacter('q');

        Run(temp, console, path);

        var output = console.Output;
        var lastFrame = output.LastIndexOf(path, StringComparison.Ordinal);
        var frame = lastFrame < 0 ? output : output[lastFrame..];

        // The confirmation panel is rendered, not cropped away.
        Assert.Contains("y confirm", output);

        // The last frame keeps the footer and the action status.
        Assert.Contains("q quit", frame);
        Assert.Contains("Removed", frame);

        // A cropped frame ends with an ellipsis.
        Assert.DoesNotContain("…", frame);
    }

    private static TestConsole StartConsole()
    {
        var console = new TestConsole();
        AnsiConsole.Console = console;
        console.Width(120);
        return console;
    }

    /// <summary>
    /// The live display keeps every frame in the test output, so the assertions look at
    /// the last one only. Every frame starts with the history file path.
    /// </summary>
    private static string Rendered(TestConsole console, string path)
    {
        var output = console.Output;
        var lastFrame = output.LastIndexOf(path, StringComparison.Ordinal);

        return lastFrame < 0 ? output : output[lastFrame..];
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
