using System.Text;
using System.Text.Json;
using FsEqual.Tool.Commands;
using FsEqual.Tool.Comparison;
using Spectre.Console;

namespace FsEqual.Tool.Interactive;

internal sealed class InteractiveCompareSession
{
    private readonly ComparisonResult _result;
    private readonly ComparisonOptions _options;

    private VerbosityLevel _verbosity;
    private Func<ComparisonEntry, bool> _filter = _ => true;
    private string _filterDescription = "All entries";

    public InteractiveCompareSession(ComparisonResult result, ComparisonOptions options)
    {
        _result = result;
        _options = options;
        _verbosity = options.Verbosity;
    }

    public void Run()
    {
        while (true)
        {
            AnsiConsole.Clear();
            RenderHeader();
            RenderSummary();
            RenderFilteredList();

            var choice = AnsiConsole.Prompt(new SelectionPrompt<string>
            {
                HighlightStyle = new Style(foreground: Color.Aqua)
            }
            .Title("Select an action")
            .AddChoices("Browse", "Filter", "Export", "Toggle Verbosity", "Help", "Quit"));

            switch (choice)
            {
                case "Browse":
                    Browse();
                    break;
                case "Filter":
                    ChooseFilter();
                    break;
                case "Export":
                    Export();
                    break;
                case "Toggle Verbosity":
                    ToggleVerbosity();
                    break;
                case "Help":
                    ShowHelp();
                    break;
                case "Quit":
                    return;
            }
        }
    }

    private void RenderHeader()
    {
        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap());
        grid.AddColumn(new GridColumn());
        grid.AddRow("Left", _options.Left);
        if (!string.IsNullOrWhiteSpace(_options.Right))
        {
            grid.AddRow("Right", _options.Right!);
        }
        if (!string.IsNullOrWhiteSpace(_options.BaselinePath))
        {
            grid.AddRow("Baseline", _options.BaselinePath!);
        }
        grid.AddRow("Mode", _options.Mode.ToString());
        grid.AddRow("Algorithm", _options.Algorithm.ToString());
        grid.AddRow("Workers", _options.MaxDegreeOfParallelism.ToString());
        grid.AddRow("Filter", _filterDescription);
        grid.AddRow("Verbosity", _verbosity.ToString());

        AnsiConsole.Write(new Panel(grid)
            .Header("fsEqual Interactive")
            .Border(BoxBorder.Rounded)
            .Expand());
    }

    private void RenderSummary()
    {
        var summary = _result.Summary;
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Metric");
        table.AddColumn("Value");
        table.AddRow("Files compared", summary.FilesCompared.ToString());
        table.AddRow("Files equal", summary.FilesEqual.ToString());
        table.AddRow("File differences", summary.FileDifferences.ToString());
        table.AddRow("Missing left", summary.MissingLeft.ToString());
        table.AddRow("Missing right", summary.MissingRight.ToString());
        table.AddRow("Directory differences", summary.DirectoryDifferences.ToString());
        table.AddRow("Baseline differences", summary.BaselineDifferences.ToString());
        table.AddRow("Issues", summary.Errors.ToString());
        table.AddRow("Duration", _result.Duration.ToString("g"));

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private void RenderFilteredList()
    {
        var entries = GetEntries().Where(_filter).ToList();
        if (entries.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No entries match the current filter.[/]");
            return;
        }

        var maxRows = _verbosity >= VerbosityLevel.Debug ? Math.Min(entries.Count, 100) : Math.Min(entries.Count, 25);
        var table = new Table().Border(TableBorder.Minimal).Expand();
        table.AddColumn("Kind");
        table.AddColumn("Status");
        table.AddColumn("Path");
        table.AddColumn("Detail");

        foreach (var entry in entries.Take(maxRows))
        {
            table.AddRow(entry.Kind.ToString(), entry.Status.ToString(), entry.Path, entry.Detail ?? string.Empty);
        }

        AnsiConsole.Write(table);
        if (entries.Count > maxRows)
        {
            AnsiConsole.MarkupLine($"Showing {maxRows} of {entries.Count} entries.");
        }
    }

    private void Browse()
    {
        var entries = GetEntries().Where(_filter).ToList();
        if (entries.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No entries to browse.[/]");
            WaitForContinue();
            return;
        }

        var prompt = new SelectionPrompt<ComparisonEntry>
        {
            PageSize = 15,
            HighlightStyle = new Style(foreground: Color.Aqua)
        }
        .Title("Select an entry to inspect")
        .UseConverter(entry => $"{entry.Kind,-9} {entry.Status,-14} {entry.Path}")
        .AddChoices(entries);

        var selection = AnsiConsole.Prompt(prompt);
        ShowEntry(selection);
    }

    private void ShowEntry(ComparisonEntry entry)
    {
        var panel = new Panel(RenderEntryDetails(entry))
            .Header(entry.Path)
            .Border(BoxBorder.Rounded)
            .Expand();
        AnsiConsole.Write(panel);
        WaitForContinue();
    }

    private static string RenderEntryDetails(ComparisonEntry entry)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Kind: {entry.Kind}");
        builder.AppendLine($"Status: {entry.Status}");
        if (!string.IsNullOrWhiteSpace(entry.Detail))
        {
            builder.AppendLine($"Detail: {entry.Detail}");
        }

        if (entry.Left is not null)
        {
            builder.AppendLine("Left:");
            AppendMetadata(builder, entry.Left);
        }

        if (entry.Right is not null)
        {
            builder.AppendLine("Right:");
            AppendMetadata(builder, entry.Right);
        }

        return builder.ToString();
    }

    private static void AppendMetadata(StringBuilder builder, FileMetadata metadata)
    {
        builder.AppendLine($"  Size: {metadata.Size} bytes");
        builder.AppendLine($"  Modified: {metadata.LastWriteTimeUtc:u}");
        if (!string.IsNullOrWhiteSpace(metadata.Hash))
        {
            builder.AppendLine($"  Hash ({metadata.HashAlgorithm}): {metadata.Hash}");
        }
        if (!string.IsNullOrWhiteSpace(metadata.Source))
        {
            builder.AppendLine($"  Source: {metadata.Source}");
        }
    }

    private void ChooseFilter()
    {
        var choice = AnsiConsole.Prompt(new SelectionPrompt<string>
        {
            Title = "Select filter",
            HighlightStyle = new Style(foreground: Color.Aqua)
        }
        .AddChoices("All", "Files only", "Directories only", "Baseline only", "Differences", "Missing", "Hash mismatches", "Size mismatches", "Time mismatches", "Errors"));

        switch (choice)
        {
            case "All":
                _filter = _ => true;
                _filterDescription = "All entries";
                break;
            case "Files only":
                _filter = entry => entry.Kind == EntryKind.File;
                _filterDescription = "File differences";
                break;
            case "Directories only":
                _filter = entry => entry.Kind == EntryKind.Directory;
                _filterDescription = "Directory differences";
                break;
            case "Baseline only":
                _filter = entry => entry.Kind == EntryKind.Baseline;
                _filterDescription = "Baseline differences";
                break;
            case "Differences":
                _filter = entry => entry.Status is EntryStatus.HashMismatch or EntryStatus.SizeMismatch or EntryStatus.TimeMismatch or EntryStatus.TypeMismatch;
                _filterDescription = "Content differences";
                break;
            case "Missing":
                _filter = entry => entry.Status is EntryStatus.MissingLeft or EntryStatus.MissingRight;
                _filterDescription = "Missing entries";
                break;
            case "Hash mismatches":
                _filter = entry => entry.Status == EntryStatus.HashMismatch;
                _filterDescription = "Hash mismatches";
                break;
            case "Size mismatches":
                _filter = entry => entry.Status == EntryStatus.SizeMismatch;
                _filterDescription = "Size mismatches";
                break;
            case "Time mismatches":
                _filter = entry => entry.Status == EntryStatus.TimeMismatch;
                _filterDescription = "Modified time mismatches";
                break;
            case "Errors":
                _filter = entry => entry.Status == EntryStatus.Error;
                _filterDescription = "Error entries";
                break;
        }
    }

    private void Export()
    {
        var format = AnsiConsole.Prompt(new SelectionPrompt<string>
        {
            Title = "Select export format",
            HighlightStyle = new Style(foreground: Color.Aqua)
        }
        .AddChoices("json", "csv", "markdown"));

        var path = AnsiConsole.Prompt(new TextPrompt<string>("Enter output path:").Validate(path => !string.IsNullOrWhiteSpace(path) ? ValidationResult.Success() : ValidationResult.Error("Path is required")));
        var fullPath = Path.GetFullPath(path);

        switch (format)
        {
            case "json":
                WriteJson(fullPath);
                break;
            case "csv":
                WriteCsv(fullPath);
                break;
            case "markdown":
                WriteMarkdown(fullPath);
                break;
        }

        AnsiConsole.MarkupLine($"[green]Exported current view to[/] {fullPath}");
        WaitForContinue();
    }

    private void WriteJson(string path)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var filtered = GetEntries().Where(_filter).ToList();
        var payload = JsonSerializer.Serialize(filtered, options);
        File.WriteAllText(path, payload);
    }

    private void WriteCsv(string path)
    {
        var filtered = GetEntries().Where(_filter).ToList();
        var builder = new StringBuilder();
        builder.AppendLine("Kind,Status,Path,LeftSize,RightSize,Detail");
        foreach (var entry in filtered)
        {
            builder.AppendLine(string.Join(',',
                Escape(entry.Kind.ToString()),
                Escape(entry.Status.ToString()),
                Escape(entry.Path),
                Escape(entry.Left?.Size.ToString() ?? string.Empty),
                Escape(entry.Right?.Size.ToString() ?? string.Empty),
                Escape(entry.Detail ?? string.Empty)));
        }

        File.WriteAllText(path, builder.ToString());
    }

    private void WriteMarkdown(string path)
    {
        var filtered = GetEntries().Where(_filter).ToList();
        var builder = new StringBuilder();
        builder.AppendLine("| Kind | Status | Path | Detail |");
        builder.AppendLine("| --- | --- | --- | --- |");
        foreach (var entry in filtered)
        {
            builder.AppendLine($"| {EscapeMarkdown(entry.Kind.ToString())} | {EscapeMarkdown(entry.Status.ToString())} | {EscapeMarkdown(entry.Path)} | {EscapeMarkdown(entry.Detail ?? string.Empty)} |");
        }

        File.WriteAllText(path, builder.ToString());
    }

    private static string Escape(string value)
    {
        return '"' + value.Replace("\"", "\"\"") + '"';
    }

    private static string EscapeMarkdown(string value)
    {
        return value.Replace("|", "\\|");
    }

    private void ToggleVerbosity()
    {
        _verbosity = _verbosity switch
        {
            VerbosityLevel.Trace => VerbosityLevel.Debug,
            VerbosityLevel.Debug => VerbosityLevel.Info,
            VerbosityLevel.Info => VerbosityLevel.Warn,
            VerbosityLevel.Warn => VerbosityLevel.Error,
            VerbosityLevel.Error => VerbosityLevel.Trace,
            _ => VerbosityLevel.Info
        };
    }

    private void ShowHelp()
    {
        var panel = new Panel("Use the menu to browse differences, filter results, export views, or adjust verbosity. The export option mirrors the spec's E key functionality.")
            .Header("Help")
            .Border(BoxBorder.Rounded)
            .Expand();
        AnsiConsole.Write(panel);
        WaitForContinue();
    }

    private void WaitForContinue()
    {
        AnsiConsole.MarkupLine("[grey]Press enter to continue...[/]");
        Console.ReadLine();
    }

    private IEnumerable<ComparisonEntry> GetEntries()
    {
        foreach (var entry in _result.Differences)
        {
            yield return entry;
        }
        foreach (var entry in _result.DirectoryDifferences)
        {
            yield return entry;
        }
        foreach (var entry in _result.BaselineDifferences)
        {
            yield return entry;
        }
        if (_result.Issues.Count > 0)
        {
            foreach (var issue in _result.Issues)
            {
                yield return new ComparisonEntry
                {
                    Kind = EntryKind.File,
                    Status = EntryStatus.Error,
                    Path = issue.Message,
                    Detail = issue.Exception?.Message
                };
            }
        }
    }
}
