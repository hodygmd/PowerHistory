namespace PowerHistory.Cli.Interactive;

using Spectre.Console;
using PowerHistory.Core.Models;
using PowerHistory.Core.Services;

/// <summary>
/// Interactive interface to browse, search and clean a history file.
/// </summary>
internal sealed class InteractiveApp
{
    private readonly CliApplication _app;
    private readonly CommandLine _command;
    private readonly HashSet<int> _selected = [];
    private readonly int _pageSize;

    private HistoryDocument? _document;
    private string _query = string.Empty;
    private bool _caseSensitive;
    private int _cursor;
    private int _scroll;
    private string? _status;

    public InteractiveApp(CliApplication app, CommandLine command)
    {
        _app = app;
        _command = command;
        _app.ConfigureBackups();
        _pageSize = Math.Max(5, app.Options.PageSize);
        _caseSensitive = app.Options.CaseSensitiveSearch;
    }

    public int Run()
    {
        if (!SelectInitialFile())
        {
            return ExitCodes.Aborted;
        }

        while (true)
        {
            Render();

            ConsoleKeyInfo key;
            try
            {
                var read = AnsiConsole.Console.Input.ReadKey(intercept: true);
                if (read is null)
                {
                    continue;
                }

                key = read.Value;
            }
            catch (InvalidOperationException)
            {
                // No interactive console available.
                return ExitCodes.Success;
            }

            if (HandleKey(key))
            {
                return ExitCodes.Success;
            }
        }
    }

    private bool SelectInitialFile()
    {
        var explicitPath = _command.Value("file");
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            if (!File.Exists(explicitPath))
            {
                Console.Error.WriteLine($"History file not found: {explicitPath}");
                return false;
            }

            Load(explicitPath);
            return true;
        }

        if (!string.IsNullOrWhiteSpace(_app.Options.DefaultHistoryPath) && File.Exists(_app.Options.DefaultHistoryPath))
        {
            Load(_app.Options.DefaultHistoryPath);
            return true;
        }

        var discovered = _app.Discovery.Discover();
        if (discovered.Count == 1)
        {
            Load(discovered[0].Path);
            return true;
        }

        var choices = discovered.Select(entry => entry.Path).ToList();
        choices.Add("Enter a path manually...");

        var selection = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select a history file")
                .PageSize(10)
                .AddChoices(choices));

        if (selection == "Enter a path manually...")
        {
            selection = AnsiConsole.Prompt(new TextPrompt<string>("Path: "));
            if (string.IsNullOrWhiteSpace(selection))
            {
                return false;
            }
        }

        if (!File.Exists(selection))
        {
            Console.Error.WriteLine($"History file not found: {selection}");
            return false;
        }

        Load(selection);
        return true;
    }

    private void Load(string path)
    {
        try
        {
            _document = _app.HistoryFile.Read(path);
            _selected.Clear();
            _cursor = 0;
            _scroll = 0;
            _status = null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _status = $"[red]{e.Message}[/]";
        }
    }

    private IReadOnlyList<HistoryEntry> VisibleEntries()
    {
        if (_document is null)
        {
            return [];
        }

        return HistoryQuery.Find(_document.Entries, _query, _caseSensitive);
    }

    private bool HandleKey(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
                MoveCursor(-1);
                return false;
            case ConsoleKey.DownArrow:
                MoveCursor(1);
                return false;
            case ConsoleKey.PageUp:
                MoveCursor(-_pageSize);
                return false;
            case ConsoleKey.PageDown:
                MoveCursor(_pageSize);
                return false;
            case ConsoleKey.Home:
                MoveCursorTo(0);
                return false;
            case ConsoleKey.End:
                MoveCursorTo(VisibleEntries().Count - 1);
                return false;
            case ConsoleKey.Spacebar:
                ToggleSelection();
                return false;
            case ConsoleKey.Enter:
                _status = null;
                return false;
            case ConsoleKey.A when key.Modifiers == ConsoleModifiers.Control:
                SelectAllVisible();
                return false;
            case ConsoleKey.Escape:
                if (!string.IsNullOrEmpty(_query))
                {
                    _query = string.Empty;
                    ResetCursor();
                }
                else
                {
                    return true;
                }

                return false;
        }

        switch (key.KeyChar)
        {
            case 'q' or 'Q':
                return true;
            case '/':
                PromptSearch();
                return false;
            case 'c' or 'C':
                _query = string.Empty;
                ResetCursor();
                return false;
            case 'd' or 'D':
                DeleteSelected();
                return false;
            case 'r' or 'R':
                Reload();
                return false;
            default:
                return false;
        }
    }

    private void MoveCursor(int delta)
    {
        var count = VisibleEntries().Count;
        if (count == 0)
        {
            _cursor = 0;
            return;
        }

        _cursor = Math.Clamp(_cursor + delta, 0, count - 1);
        EnsureCursorVisible(count);
    }

    private void MoveCursorTo(int index)
    {
        var count = VisibleEntries().Count;
        if (count == 0)
        {
            _cursor = 0;
            return;
        }

        _cursor = Math.Clamp(index, 0, count - 1);
        EnsureCursorVisible(count);
    }

    private void EnsureCursorVisible(int count)
    {
        if (_cursor < _scroll)
        {
            _scroll = _cursor;
        }
        else if (_cursor >= _scroll + _pageSize)
        {
            _scroll = _cursor - _pageSize + 1;
        }

        _scroll = Math.Clamp(_scroll, 0, Math.Max(0, count - _pageSize));
    }

    private void ResetCursor()
    {
        _cursor = 0;
        _scroll = 0;
    }

    private void ToggleSelection()
    {
        var visible = VisibleEntries();
        if (_cursor >= visible.Count)
        {
            return;
        }

        var index = visible[_cursor].Index;
        if (!_selected.Remove(index))
        {
            _selected.Add(index);
        }
    }

    private void SelectAllVisible()
    {
        foreach (var entry in VisibleEntries())
        {
            _selected.Add(entry.Index);
        }

        _status = $"Selected {_selected.Count} entries.";
    }

    private void PromptSearch()
    {
        var prompt = new TextPrompt<string>("search: ")
        {
            AllowEmpty = true,
            ShowDefaultValue = false,
        };

        var value = AnsiConsole.Prompt(prompt);
        _query = value ?? string.Empty;
        ResetCursor();
        _status = null;
    }

    private void Reload()
    {
        if (_document is null)
        {
            return;
        }

        var path = _document.SourcePath;
        _document = _app.HistoryFile.Read(path);
        _selected.Clear();
        ResetCursor();
        _status = $"[grey]Reloaded {path}[/]";
    }

    private void DeleteSelected()
    {
        if (_document is null || _selected.Count == 0)
        {
            _status = "[yellow]Nothing selected.[/]";
            return;
        }

        var entries = _document.Entries.Where(entry => _selected.Contains(entry.Index)).ToList();
        RenderDeletePreview(entries);

        if (!AnsiConsole.Confirm($"Delete {entries.Count} entries from {_document.SourcePath}?", false))
        {
            _status = "[yellow]Cancelled.[/]";
            return;
        }

        var remaining = HistoryCleaner.Remove(_document.Entries, _selected);
        var removed = entries.Count;

        if (_app.HistoryFile.HasChangedSince(_document.SourcePath, _document.ContentHash))
        {
            _status = "[red]The file changed since it was read. Nothing was written.[/]";
            return;
        }

        try
        {
            var backup = _app.Backups.Create(_document.SourcePath);
            _app.HistoryFile.Write(_document with { Entries = remaining });
            Reload();
            _status = $"[green]Removed {removed} entries.[/] [grey]Backup: {backup.Path}[/]";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _status = $"[red]{e.Message}[/]";
        }
    }

    private void Render()
    {
        AnsiConsole.Clear();

        if (_document is null)
        {
            AnsiConsole.MarkupLine("[red]No history file loaded.[/]");
            return;
        }

        var visible = VisibleEntries();
        var width = Math.Max(40, AnsiConsole.Profile.Width - 6);
        var commandWidth = Math.Max(20, width - 12);

        AnsiConsole.MarkupLine($"[bold]PowerHistory[/] [grey]{_app.Version}[/]");
        AnsiConsole.MarkupLine($"[grey]{Markup.Escape(_document.SourcePath)}[/]");
        AnsiConsole.MarkupLine(
            $"[grey]{_document.Entries.Count} entries · {visible.Count} shown · {_selected.Count} selected[/]");

        var search = string.IsNullOrEmpty(_query)
            ? "[grey](no filter)[/]"
            : $"[bold]{Markup.Escape(_query)}[/]";
        AnsiConsole.MarkupLine($"Filter: {search}{( _caseSensitive ? " [grey](case sensitive)[/]" : string.Empty)}");

        if (!string.IsNullOrEmpty(_status))
        {
            AnsiConsole.MarkupLine(_status);
        }

        AnsiConsole.WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("").NoWrap())
            .AddColumn(new TableColumn("").NoWrap())
            .AddColumn(new TableColumn("#").RightAligned().NoWrap())
            .AddColumn(new TableColumn("Command").NoWrap());

        var last = Math.Min(_scroll + _pageSize, visible.Count);
        for (var i = _scroll; i < last; i++)
        {
            var entry = visible[i];
            var isCursor = i == _cursor;
            var marker = isCursor ? "[yellow]>[/]" : " ";
            var box = _selected.Contains(entry.Index) ? "[green]x[/]" : " ";
            var text = Markup.Escape(Truncate(entry.Text, commandWidth));
            var styled = isCursor ? $"[black on yellow]{text}[/]" : text;

            table.AddRow(marker, box, entry.Index.ToString(), styled);
        }

        if (visible.Count == 0)
        {
            table.AddRow(string.Empty, string.Empty, string.Empty, "[grey](no entries)[/]");
        }

        AnsiConsole.Write(table);

        if (_cursor < visible.Count)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[grey]Selected entry:[/]");
            AnsiConsole.WriteLine(visible[_cursor].Text);
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(
            "[grey]↑/↓ move · space select · ctrl+a all · / filter · c clear · d delete · r reload · q quit[/]");
    }

    private void RenderDeletePreview(IReadOnlyList<HistoryEntry> entries)
    {
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine($"[bold]Delete preview[/] [grey]{entries.Count} entries[/]");

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("#").RightAligned().NoWrap())
            .AddColumn(new TableColumn("Command").NoWrap());

        foreach (var entry in entries.Take(20))
        {
            table.AddRow(entry.Index.ToString(), Markup.Escape(Truncate(entry.Text, Math.Max(20, AnsiConsole.Profile.Width - 20))));
        }

        AnsiConsole.Write(table);

        if (entries.Count > 20)
        {
            AnsiConsole.MarkupLine($"[grey]... and {entries.Count - 20} more[/]");
        }

        AnsiConsole.MarkupLine("[grey]A backup is created before the file is written.[/]");
        AnsiConsole.WriteLine();
    }

    private static string Truncate(string text, int width)
    {
        if (text.Length <= width)
        {
            return text;
        }

        return string.Concat(text.AsSpan(0, Math.Max(0, width - 1)), "…");
    }
}
