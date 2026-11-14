namespace PowerHistory.Cli;

/// <summary>
/// Minimal command line parser: one command, positional arguments and named options.
/// </summary>
internal sealed class CommandLine
{
    private static readonly HashSet<string> Flags = new(StringComparer.OrdinalIgnoreCase)
    {
        "yes", "y", "help", "h", "version", "v",
        "duplicates", "empty", "case-sensitive", "exact",
        "keep-first-occurrence", "keep-last-occurrence",
    };

    private CommandLine(string? command, IReadOnlyList<string> positional, IReadOnlyDictionary<string, string?> options)
    {
        Command = command;
        Positional = positional;
        Options = options;
    }

    public string? Command { get; }

    public IReadOnlyList<string> Positional { get; }

    public IReadOnlyDictionary<string, string?> Options { get; }

    public static CommandLine Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        string? command = null;
        var positional = new List<string>();
        var options = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg.StartsWith("--", StringComparison.Ordinal) && arg.Length > 2)
            {
                var name = arg[2..];
                string? value = null;

                var separator = name.IndexOf('=');
                if (separator >= 0)
                {
                    value = name[(separator + 1)..];
                    name = name[..separator];
                }
                else if (!Flags.Contains(name) && i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    value = args[++i];
                }

                options[name] = value;
                continue;
            }

            if (arg.StartsWith('-') && arg.Length > 1)
            {
                options[arg[1..]] = null;
                continue;
            }

            if (command is null)
            {
                command = arg;
            }
            else
            {
                positional.Add(arg);
            }
        }

        return new CommandLine(command, positional, options);
    }

    public bool Has(string name) => Options.ContainsKey(name);

    public string? Value(string name) => Options.TryGetValue(name, out var value) ? value : null;

    public bool Flag(string name) => Options.ContainsKey(name);

    public string? PositionalAt(int index) => index < Positional.Count ? Positional[index] : null;
}
