namespace PowerHistory.Cli.Interactive;

using PowerHistory.Core.Services;
using Spectre.Console.Rendering;
using PowerHistory.Core.Models;
using Spectre.Console;
using System.Text;

/// <summary>
/// Interactive interface to browse, search and clean a history file.
/// </summary>
/// <remarks>
/// Rendering goes through <see cref="LiveDisplay"/> on the alternate screen buffer
/// </remarks>
internal sealed class InteractiveApp
{
    private const string Accent = "cyan1";
    private const string Dim = "grey";
    private const string Good = "green";
    private const string Warn = "yellow";
    private const string Bad = "red";

    /// <summary>
    /// Lines the table itself uses: top border, header row, separator and bottom border.
    /// </summary>
    private const int TableChrome = 4;

    private readonly CliApplication _app;
    private readonly CommandLine _command;
    private readonly HashSet<int> _selected = [];
    private readonly int _configuredPageSize;

    private HistoryDocument? _document;
    private string _query = string.Empty;
    private bool _caseSensitive;
    private int _cursor;
    private int _scroll;
    private string? _status;

    // Live terminal size, refreshed on every frame so the view follows window resizes.
    private int _terminalWidth = 80;
    private int _terminalHeight = 24;
    private int _pageSize = 10;

    // The filtered view is expensive on large files; only rebuild it when the filter changes.
    private IReadOnlyList<HistoryEntry> _visible = [];
    private bool _visibleDirty = true;

    private Mode _mode = Mode.Browse;
    private string _draft = string.Empty;
    private string _queryBeforeEdit = string.Empty;

    public InteractiveApp(CliApplication app, CommandLine command)
    {
        _app = app;
        _command = command;
        _app.ConfigureBackups();
        _configuredPageSize = Math.Max(0, app.Options.PageSize);
        _caseSensitive = app.Options.CaseSensitiveSearch;
    }

    private enum Mode
    {
        Browse,
        Search,
        Confirm,
    }

    public int Run()
    {
        if (!SelectInitialFile())
        {
            return ExitCodes.Aborted;
        }

        AnsiConsole.Console.AlternateScreen(() =>
        {
            AnsiConsole.Cursor.Hide();
            try
            {
                AnsiConsole.Live(BuildView())
                    .AutoClear(false)
                    .Overflow(VerticalOverflow.Ellipsis)
                    .Cropping(VerticalOverflowCropping.Bottom)
                    .Start(RunLoop);
            }
            finally
            {
                AnsiConsole.Cursor.Show();
            }
        });

        return ExitCodes.Success;
    }

    private void RunLoop(LiveDisplayContext context)
    {
        while (true)
        {
            context.UpdateTarget(BuildView());
            context.Refresh();

            if (!TryReadKey(out var key))
            {
                return;
            }

            if (HandleKey(key))
            {
                return;
            }
        }
    }

    private static bool TryReadKey(out ConsoleKeyInfo key)
    {
        try
        {
            var read = AnsiConsole.Console.Input.ReadKey(intercept: true);
            key = read ?? default;
            return read is not null;
        }
        catch (InvalidOperationException)
        {
            // No interactive console available.
            key = default;
            return false;
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
            _visibleDirty = true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _status = $"[{Bad}]{Markup.Escape(e.Message)}[/]";
        }
    }

    private IReadOnlyList<HistoryEntry> VisibleEntries()
    {
        if (!_visibleDirty)
        {
            return _visible;
        }

        _visible = _document is null
            ? []
            : HistoryQuery.Find(_document.Entries, _query, _caseSensitive);
        _visibleDirty = false;
        return _visible;
    }

    private void SetQuery(string query)
    {
        if (!string.Equals(_query, query, StringComparison.Ordinal))
        {
            _query = query;
            _visibleDirty = true;
        }

        ResetCursor();
    }

    private int PageSize => _pageSize;

    private static (int Width, int Height) TerminalSize()
    {
        var profile = AnsiConsole.Profile;

        if (profile.Capabilities.Interactive && !Console.IsOutputRedirected)
        {
            try
            {
                var width = Console.WindowWidth;
                var height = Console.WindowHeight;
                if (width > 0 && height > 0)
                {
                    return (width, height);
                }
            }
            catch (IOException)
            {
            }
            catch (PlatformNotSupportedException)
            {
            }
        }

        return (profile.Width, profile.Height);
    }

    private bool HandleKey(ConsoleKeyInfo key)
    {
        switch (_mode)
        {
            case Mode.Search:
                HandleSearchKey(key);
                return false;
            case Mode.Confirm:
                HandleConfirmKey(key);
                return false;
        }

        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
                MoveCursor(-1);
                return false;
            case ConsoleKey.DownArrow:
                MoveCursor(1);
                return false;
            case ConsoleKey.PageUp:
                MoveCursor(-PageSize);
                return false;
            case ConsoleKey.PageDown:
                MoveCursor(PageSize);
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
            case ConsoleKey.A when (key.Modifiers & ConsoleModifiers.Control) != 0:
                SelectAllVisible();
                return false;
            case ConsoleKey.Escape:
                if (!string.IsNullOrEmpty(_query))
                {
                    SetQuery(string.Empty);
                    return false;
                }

                return true;
        }

        switch (char.ToLowerInvariant(key.KeyChar))
        {
            case 'q':
                return true;
            case '/':
                BeginSearch();
                return false;
            case 'c':
                SetQuery(string.Empty);
                _status = null;
                return false;
            case 'd':
                RequestDelete();
                return false;
            case 'r':
                Reload();
                return false;
            default:
                return false;
        }
    }

    private void BeginSearch()
    {
        _queryBeforeEdit = _query;
        _draft = _query;
        _mode = Mode.Search;
        _status = null;
    }

    private void HandleSearchKey(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.Enter:
                _mode = Mode.Browse;
                return;
            case ConsoleKey.Escape:
                SetQuery(_queryBeforeEdit);
                _mode = Mode.Browse;
                return;
            case ConsoleKey.Backspace:
                if (_draft.Length > 0)
                {
                    _draft = _draft[..^1];
                    SetQuery(_draft);
                }

                return;
        }

        if (key.KeyChar >= ' ' && key.KeyChar != '\u007f')
        {
            _draft += key.KeyChar;
            SetQuery(_draft);
        }
    }

    private void HandleConfirmKey(ConsoleKeyInfo key)
    {
        _mode = Mode.Browse;

        if (key.KeyChar is 'y' or 'Y')
        {
            ApplyDelete();
        }
        else
        {
            _status = $"[{Warn}]Cancelled.[/]";
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
        var pageSize = PageSize;

        if (_cursor < _scroll)
        {
            _scroll = _cursor;
        }
        else if (_cursor >= _scroll + pageSize)
        {
            _scroll = _cursor - pageSize + 1;
        }

        _scroll = Math.Clamp(_scroll, 0, Math.Max(0, count - pageSize));
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

        _status = $"[{Good}]Selected {_selected.Count} entries.[/]";
    }

    private void Reload()
    {
        if (_document is null)
        {
            return;
        }

        var path = _document.SourcePath;
        Load(path);
        _status = $"[{Dim}]Reloaded {Markup.Escape(path)}[/]";
    }

    private void RequestDelete()
    {
        if (_document is null || _selected.Count == 0)
        {
            _status = $"[{Warn}]Nothing selected.[/]";
            return;
        }

        _mode = Mode.Confirm;
    }

    private void ApplyDelete()
    {
        if (_document is null)
        {
            return;
        }

        var path = _document.SourcePath;
        var hash = _document.ContentHash;
        var remaining = HistoryCleaner.Remove(_document.Entries, _selected);
        var removed = _document.Entries.Count - remaining.Count;

        if (_app.HistoryFile.HasChangedSince(path, hash))
        {
            _status = $"[{Bad}]The file changed since it was read. Nothing was written.[/]";
            return;
        }

        try
        {
            var backup = _app.Backups.Create(path);
            _app.HistoryFile.Write(_document with { Entries = remaining });
            Load(path);
            _status = $"[{Good}]Removed {removed} entries.[/] [{Dim}]Backup: {Markup.Escape(backup.Path)}[/]";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _status = $"[{Bad}]{Markup.Escape(e.Message)}[/]";
        }
    }

    private IRenderable BuildView()
    {
        var (width, height) = TerminalSize();
        _terminalWidth = width;
        _terminalHeight = height > 0 ? height : 24;

        var overhead = CountLines(BuildChrome(), _terminalWidth, _terminalHeight) + TableChrome;
        var rows = _terminalHeight - overhead;

        if (_configuredPageSize > 0)
        {
            rows = Math.Min(rows, _configuredPageSize);
        }

        rows = Math.Max(3, rows);

        while (rows > 3)
        {
            var frame = BuildFrame(rows);
            if (CountLines(frame, _terminalWidth, _terminalHeight) <= _terminalHeight)
            {
                _pageSize = rows;
                return frame;
            }

            rows--;
        }

        _pageSize = rows;
        return BuildFrame(rows);
    }

    private IRenderable BuildFrame(int rows) =>
        new Rows(
            new Markup(BuildHeader()),
            BuildTable(rows),
            new Markup(BuildDetails()),
            new Markup(BuildFooter()));

    private IRenderable BuildChrome() =>
        new Rows(
            new Markup(BuildHeader()),
            new Markup(BuildDetails()),
            new Markup(BuildFooter()));

    /// <summary>
    /// Counts the lines a renderable takes, wrapping included.
    /// </summary>
    private static int CountLines(IRenderable renderable, int width, int height)
    {
        var options = new RenderOptions(AnsiConsole.Profile.Capabilities, new Size(width, height));

        var lines = 1;
        foreach (var segment in renderable.Render(options, width))
        {
            if (segment.IsLineBreak)
            {
                lines++;
            }
        }

        return lines;
    }

    private string BuildHeader()
    {
        var total = _document?.Entries.Count ?? 0;
        var shown = VisibleEntries().Count;
        var selected = _selected.Count;

        var selectedText = selected > 0
            ? $"[{Warn}]{selected}[/] [{Dim}]selected[/]"
            : $"[{Dim}]0 selected[/]";

        var builder = new StringBuilder();
        builder.Append($"[bold {Accent}]PowerHistory[/] [{Dim}]{Markup.Escape(ShortVersion)}[/]\n");
        builder.Append($"[{Dim}]{Markup.Escape(_document?.SourcePath ?? "(no file)")}[/]\n");
        builder.Append($"[white]{total}[/] [{Dim}]entries ·[/] [white]{shown}[/] [{Dim}]shown ·[/] {selectedText}");

        if (_mode == Mode.Search)
        {
            builder.Append($" [{Dim}]filter:[/] [bold {Warn}]{Markup.Escape(_draft)}[/][{Accent}]▏[/]");
        }
        else if (!string.IsNullOrEmpty(_query))
        {
            builder.Append($" [{Dim}]filter:[/] [bold {Warn}]{Markup.Escape(_query)}[/]");
        }
        else if (_caseSensitive)
        {
            builder.Append($" [{Dim}](case sensitive)[/]");
        }

        if (!string.IsNullOrEmpty(_status))
        {
            builder.Append('\n').Append(_status);
        }

        return builder.ToString();
    }

    private IRenderable BuildTable(int pageSize)
    {
        var visible = VisibleEntries();
        var commandWidth = Math.Max(20, _terminalWidth - 20);
        _scroll = Math.Clamp(_scroll, 0, Math.Max(0, visible.Count - pageSize));

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn(new TableColumn(" ").NoWrap())
            .AddColumn(new TableColumn(" ").NoWrap())
            .AddColumn(new TableColumn("#").RightAligned().NoWrap())
            .AddColumn(new TableColumn("Command").NoWrap());

        table.BorderStyle = new Style(foreground: Color.Grey);

        if (visible.Count == 0)
        {
            table.AddRow(" ", " ", " ", $"[{Dim}](no entries)[/]");
            return table;
        }

        var last = Math.Min(_scroll + pageSize, visible.Count);
        for (var i = _scroll; i < last; i++)
        {
            var entry = visible[i];
            var isCursor = i == _cursor;
            var isSelected = _selected.Contains(entry.Index);
            var text = Truncate(entry.Text, commandWidth);

            var marker = isCursor ? $"[bold {Accent}]{CursorGlyph}[/]" : " ";
            var box = isSelected ? $"[{Good}]{CheckGlyph}[/]" : " ";
            var number = isCursor ? $"[white]{entry.Index}[/]" : $"[{Dim}]{entry.Index}[/]";
            var label = isCursor
                ? $"[black on {Accent}]{Markup.Escape(text)}[/]"
                : Highlight(text, _query);

            table.AddRow(marker, box, number, label);
        }

        return table;
    }

    private string BuildDetails()
    {
        var visible = VisibleEntries();
        if (_cursor >= visible.Count)
        {
            return $"\n[{Dim}]No entry selected.[/]";
        }

        return $"\n[{Dim}]Selected:[/] {Markup.Escape(visible[_cursor].Text)}";
    }

    private string BuildFooter()
    {
        if (_mode == Mode.Confirm)
        {
            return BuildConfirmBar();
        }

        if (_mode == Mode.Search)
        {
            return $"\n[{Dim}]type to filter · [/][{Accent}]enter[/][{Dim}] apply · [/][{Accent}]esc[/][{Dim}] cancel[/]";
        }

        return $"\n[{Accent}]↑↓[/][{Dim}] move · [/]" +
               $"[{Accent}]space[/][{Dim}] select · [/]" +
               $"[{Accent}]ctrl+a[/][{Dim}] all · [/]" +
               $"[{Accent}]/[/][{Dim}] filter · [/]" +
               $"[{Accent}]c[/][{Dim}] clear · [/]" +
               $"[{Accent}]d[/][{Dim}] delete · [/]" +
               $"[{Accent}]r[/][{Dim}] reload · [/]" +
               $"[{Accent}]q[/][{Dim}] quit[/]";
    }

    private string BuildConfirmBar()
    {
        var count = _selected.Count;
        var noun = count == 1 ? "entry" : "entries";

        var preview = _document is null
            ? []
            : _document.Entries
                .Where(entry => _selected.Contains(entry.Index))
                .Take(3)
                .Select(entry => $"[{Dim}]{Markup.Escape(Truncate(entry.Text, 40))}[/]")
                .ToList();

        var builder = new StringBuilder();
        builder.Append($"\n[bold {Bad}]Delete {count} {noun}?[/] [{Dim}]A backup is created first.[/]");

        if (preview.Count > 0)
        {
            builder.Append('\n').Append("  ").Append(string.Join("   ", preview));
        }

        builder.Append($"\n[{Good}]y[/][{Dim}] confirm · [/][{Bad}]any other key[/][{Dim}] cancel[/]");
        return builder.ToString();
    }

    private static bool Unicode => AnsiConsole.Profile.Capabilities.Unicode;

    private static string CursorGlyph => Unicode ? "❯" : ">";

    private static string CheckGlyph => Unicode ? "✓" : "x";

    private static string Ellipsis => Unicode ? "…" : "...";

    private string ShortVersion
    {
        get
        {
            var version = _app.Version;
            var plus = version.IndexOf('+', StringComparison.Ordinal);
            return plus < 0 ? version : version[..plus];
        }
    }

    private string Highlight(string text, string query)
    {
        if (string.IsNullOrEmpty(query))
        {
            return Markup.Escape(text);
        }

        var comparison = _caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var result = new StringBuilder();
        var position = 0;

        while (position < text.Length)
        {
            var match = text.IndexOf(query, position, comparison);
            if (match < 0)
            {
                break;
            }

            result.Append(Markup.Escape(text[position..match]));
            result.Append($"[bold {Warn}]")
                .Append(Markup.Escape(text[match..(match + query.Length)]))
                .Append("[/]");
            position = match + query.Length;
        }

        result.Append(Markup.Escape(text[position..]));
        return result.ToString();
    }

    private static string Truncate(string text, int width)
    {
        if (text.Length <= width)
        {
            return text;
        }

        var suffix = Ellipsis;
        return string.Concat(text.AsSpan(0, Math.Max(0, width - suffix.Length)), suffix);
    }
}
