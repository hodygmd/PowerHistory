namespace PowerHistory.Cli.Tests;

public class CommandLineTests
{
    [Fact]
    public void Parse_ReadsTheCommandAndThePositionalArguments()
    {
        var command = CommandLine.Parse(["search", "docker", "ps"]);

        Assert.Equal("search", command.Command);
        Assert.Equal(["docker", "ps"], command.Positional);
    }

    [Fact]
    public void Parse_ReadsAnOptionWithItsValue()
    {
        var command = CommandLine.Parse(["list", "--file", "C:\\history.txt"]);

        Assert.Equal("list", command.Command);
        Assert.Equal("C:\\history.txt", command.Value("file"));
    }

    [Fact]
    public void Parse_ReadsAnOptionWrittenWithAnEqualSign()
    {
        var command = CommandLine.Parse(["list", "--file=C:\\history.txt"]);

        Assert.Equal("C:\\history.txt", command.Value("file"));
    }

    [Fact]
    public void Parse_RecognisesFlags()
    {
        var command = CommandLine.Parse(["clean", "--duplicates", "--yes"]);

        Assert.True(command.Flag("duplicates"));
        Assert.True(command.Flag("yes"));
    }

    [Fact]
    public void Parse_DoesNotGiveAValueToAFlag()
    {
        var command = CommandLine.Parse(["clean", "--yes", "text"]);

        Assert.True(command.Flag("yes"));
        Assert.Equal(["text"], command.Positional);
    }

    [Fact]
    public void Parse_KeepsTheValueOfAnOptionThatFollowsAFlag()
    {
        var command = CommandLine.Parse(["clean", "--yes", "--text", "docker"]);

        Assert.Equal("docker", command.Value("text"));
    }

    [Fact]
    public void Parse_WithoutArgumentsHasNoCommand()
    {
        var command = CommandLine.Parse([]);

        Assert.Null(command.Command);
        Assert.Empty(command.Positional);
    }
}
