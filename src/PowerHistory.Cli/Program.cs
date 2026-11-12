namespace PowerHistory.Cli;

internal static class Program
{
    private static int Main(string[] args)
    {
        var application = new CliApplication();
        return application.Run(args);
    }
}
