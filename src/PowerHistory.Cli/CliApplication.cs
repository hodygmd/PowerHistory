namespace PowerHistory.Cli;

using System.Reflection;
using PowerHistory.Cli.Commands;
using PowerHistory.Cli.Interactive;
using PowerHistory.Core.Interfaces;
using PowerHistory.Core.Models;
using PowerHistory.Core.Services;
using PowerHistory.Infrastructure.Backup;
using PowerHistory.Infrastructure.Configuration;
using PowerHistory.Infrastructure.Discovery;
using PowerHistory.Infrastructure.History;

/// <summary>
/// Entry point of the command line application.
/// </summary>
internal sealed class CliApplication
{
    private readonly OptionsStore _optionsStore;
    private readonly FileHistoryRepository _historyFile = new();
    private readonly PowerShellHistoryDiscovery _discovery = new();
    private readonly string? _backupDirectory;

    private PowerHistoryOptions _options = new();

    public CliApplication(string? optionsPath = null, string? backupDirectory = null)
    {
        _optionsStore = new OptionsStore(optionsPath);
        _backupDirectory = backupDirectory;
    }

    public int Run(string[] args)
    {
        try
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
        }
        catch (Exception e) when (e is IOException or PlatformNotSupportedException)
        {
            // A redirected console may refuse the change; the default encoding is still usable.
        }

        _options = _optionsStore.Load();
        var command = CommandLine.Parse(args);

        if (command.Flag("help") || command.Flag("h") || command.Command is "help")
        {
            PrintHelp();
            return ExitCodes.Success;
        }

        if (command.Flag("version") || command.Flag("v") || command.Command is "version")
        {
            Console.WriteLine(Version);
            return ExitCodes.Success;
        }

        try
        {
            return command.Command switch
            {
                null => RunInteractive(command),
                "list" => CommandHandlers.List(this, command),
                "search" => CommandHandlers.Search(this, command),
                "stats" => CommandHandlers.Stats(this, command),
                "backup" => CommandHandlers.Backup(this, command),
                "backups" => CommandHandlers.Backups(this, command),
                "restore" => CommandHandlers.Restore(this, command),
                "clean" => CommandHandlers.Clean(this, command),
                _ => UnknownCommand(command.Command),
            };
        }
        catch (FileNotFoundException e)
        {
            Console.Error.WriteLine($"Error: {e.Message}");
            return ExitCodes.FileError;
        }
        catch (DirectoryNotFoundException e)
        {
            Console.Error.WriteLine($"Error: {e.Message}");
            return ExitCodes.FileError;
        }
        catch (UnauthorizedAccessException e)
        {
            Console.Error.WriteLine($"Error: access denied: {e.Message}");
            return ExitCodes.FileError;
        }
        catch (IOException e)
        {
            Console.Error.WriteLine($"Error: {e.Message}");
            return ExitCodes.FileError;
        }
        catch (ArgumentException e)
        {
            Console.Error.WriteLine($"Error: {e.Message}");
            return ExitCodes.UsageError;
        }
        catch (InvalidOperationException e)
        {
            Console.Error.WriteLine($"Error: {e.Message}");
            return ExitCodes.Error;
        }
    }

    public string Version =>
        typeof(CliApplication).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "0.1.0";

    public PowerHistoryOptions Options => _options;

    public IHistoryFile HistoryFile => _historyFile;

    public IHistoryDiscovery Discovery => _discovery;

    public IBackupService Backups { get; private set; } = new BackupService(BackupService.DefaultDirectory);

    /// <summary>
    /// Resolves the history file to work with: explicit argument, configured default, or the
    /// first history discovered on the machine.
    /// </summary>
    public string? ResolveHistoryPath(CommandLine command, bool report = true)
    {
        var explicitPath = command.Value("file");
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            var full = Path.GetFullPath(explicitPath);
            if (!File.Exists(full))
            {
                throw new FileNotFoundException($"History file not found: {full}", full);
            }

            return full;
        }

        if (!string.IsNullOrWhiteSpace(_options.DefaultHistoryPath) && File.Exists(_options.DefaultHistoryPath))
        {
            return Path.GetFullPath(_options.DefaultHistoryPath);
        }

        var discovered = _discovery.Discover();
        var first = discovered.FirstOrDefault();
        if (first is not null)
        {
            return first.Path;
        }

        if (report)
        {
            Console.Error.WriteLine("No PowerShell history was found on this machine.");
            Console.Error.WriteLine("Point at a file explicitly with --file <path>.");
        }

        return null;
    }

    public void ConfigureBackups()
    {
        var directory = _backupDirectory
            ?? (string.IsNullOrWhiteSpace(_options.BackupDirectory)
                ? BackupService.DefaultDirectory
                : _options.BackupDirectory);

        Backups = new BackupService(directory);
    }

    private int RunInteractive(CommandLine command)
    {
        if (Console.IsInputRedirected)
        {
            PrintHelp();
            return ExitCodes.Success;
        }

        var app = new InteractiveApp(this, command);
        return app.Run();
    }

    private static int UnknownCommand(string? command)
    {
        Console.Error.WriteLine($"Unknown command '{command}'.");
        PrintHelp();
        return ExitCodes.UsageError;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            PowerHistory - administrador interactivo del historial de PowerShell

            Uso:
              histo                       Abrir la interfaz interactiva
              histo list [--file <ruta>]  Listar las entradas de un historial
              histo search <texto>        Buscar un texto en el historial
              histo stats                 Mostrar estadísticas del historial
              histo backup                Crear un backup del historial
              histo backups               Listar los backups disponibles
              histo restore <id>          Restaurar un backup sobre el historial
              histo clean [filtros]       Eliminar entradas (requiere --yes)

            Opciones:
              --file <ruta>       Archivo de historial sobre el que operar
              --limit <n>         Cantidad máxima de entradas a imprimir (list, search)
              --case-sensitive    Buscar y comparar respetando mayúsculas
              --yes               Confirmar una operación destructiva sin preguntar

            Filtros de clean:
              --duplicates        Eliminar comandos duplicados exactos
              --text <texto>      Eliminar las entradas que contienen el texto
              --exact             Con --text, exigir que el comando sea idéntico
              --regex <patrón>    Eliminar las entradas que coinciden con la expresión regular
              --empty             Eliminar las entradas vacías
              --keep-last <n>     Conservar solo las últimas n entradas

            Los filtros se combinan: solo se elimina la entrada que cumple todos.

            Otros:
              --help              Mostrar esta ayuda
              --version           Mostrar la versión
            """);
    }
}
