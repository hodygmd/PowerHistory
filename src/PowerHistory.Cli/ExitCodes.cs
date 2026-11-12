namespace PowerHistory.Cli;

/// <summary>
/// Process exit codes.
/// </summary>
internal static class ExitCodes
{
    public const int Success = 0;
    public const int Error = 1;
    public const int UsageError = 2;
    public const int FileError = 3;
    public const int Aborted = 4;
    public const int ExternalChange = 5;
}
