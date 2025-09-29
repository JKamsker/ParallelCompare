using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using FsEqual.Tool.Commands;
using FsEqual.Tool.Core;
using Spectre.Console;

namespace FsEqual.Tool.Interactive;

internal sealed class InteractiveSession
{
    private static readonly DifferenceType?[] FilterCycle =
    {
        null,
        DifferenceType.MissingLeft,
        DifferenceType.MissingRight,
        DifferenceType.TypeMismatch,
        DifferenceType.SizeMismatch,
        DifferenceType.HashMismatch,
        DifferenceType.MetadataMismatch,
    };

    private readonly ComparisonResult _result;
    private readonly ResolvedCompareSettings _settings;
    private readonly ConsoleLogger _logger;

    public InteractiveSession(ComparisonResult result, ResolvedCompareSettings settings, ConsoleLogger logger)
    {
        _result = result;
        _settings = settings;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken token)
    {
        var filterIndex = 0;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var filter = FilterCycle[filterIndex];

            AnsiConsole.Clear();
            RenderHeader(filter);
            RenderSummary();
            RenderDifferences(filter);
            RenderFooter();

            var key = Console.ReadKey(true);
            switch (key.Key)
            {
                case ConsoleKey.Q:
                    return;
                case ConsoleKey.F:
                    filterIndex = (filterIndex + 1) % FilterCycle.Length;
                    break;
                case ConsoleKey.E:
                    await ExportAsync(filter, token);
                    break;
                case ConsoleKey.H:
                case ConsoleKey.Oem2:
                    RenderHelp();
                    break;
                default:
                    break;
            }
        }
    }

    private void RenderHeader(DifferenceType? filter)
    {
        var header = new Panel($"[bold]{Markup.Escape(_settings.LeftPath)}[/] ⟷ [bold]{Markup.Escape(_settings.RightPath)}[/]\nMode: {_settings.Mode} | Algorithm: {_settings.Algorithm} | Threads: {_settings.Threads}")
        {
            Header = new PanelHeader("fsEqual Interactive"),
        };

        AnsiConsole.Write(header);
        var filterText = filter.HasValue ? $"Filter: {filter.Value}" : "Filter: All";
        AnsiConsole.MarkupLine($"[grey]{filterText}[/]");
    }

    private void RenderSummary()
    {
        var summary = _result.Summary;
        var table = new Table().Border(TableBorder.Square).AddColumns("Metric", "Value");
        table.AddRow("Outcome", _result.Outcome.ToString());
        table.AddRow("Duration", _result.Duration.ToString());
        table.AddRow("Files Compared", summary.FilesCompared.ToString());
        table.AddRow("Equal Files", summary.EqualFiles.ToString());
        table.AddRow("Directories Compared", summary.DirectoriesCompared.ToString());
        table.AddRow("Equal Directories", summary.EqualDirectories.ToString());
        table.AddRow("Missing Left", summary.MissingLeft.ToString());
        table.AddRow("Missing Right", summary.MissingRight.ToString());
        table.AddRow("Size Mismatches", summary.SizeMismatches.ToString());
        table.AddRow("Hash Mismatches", summary.HashMismatches.ToString());
        table.AddRow("Metadata Mismatches", summary.MetadataMismatches.ToString());
        table.AddRow("Errors", _result.Errors.Count.ToString());

        AnsiConsole.Write(table);
    }

    private void RenderDifferences(DifferenceType? filter)
    {
        var filtered = filter.HasValue
            ? _result.Differences.Where(d => d.Type == filter.Value).ToList()
            : _result.Differences.ToList();

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumns("Type", "Path", "Detail");

        foreach (var diff in filtered.Take(20))
        {
            table.AddRow(diff.Type.ToString(), diff.RelativePath, diff.Detail ?? string.Empty);
        }

        if (filtered.Count > 20)
        {
            table.Caption($"Showing first 20 of {filtered.Count} differences");
        }
        else if (filtered.Count == 0)
        {
            table.Caption("No differences for current filter");
        }

        AnsiConsole.Write(table);
    }

    private static void RenderFooter()
    {
        AnsiConsole.MarkupLine("[grey]Commands: [F]ilter  [E]xport  [H]elp  [Q]uit[/]");
    }

    private void RenderHelp()
    {
        AnsiConsole.Clear();
        var panel = new Panel("F - Cycle difference filter\nE - Export current view\nH - Show this help\nQ - Quit interactive mode")
        {
            Header = new PanelHeader("Help"),
        };
        AnsiConsole.Write(panel);
        AnsiConsole.MarkupLine("Press any key to continue...");
        Console.ReadKey(true);
    }

    private async Task ExportAsync(DifferenceType? filter, CancellationToken token)
    {
        var format = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title("Select export format")
            .AddChoices("json", "csv", "markdown"));

        var timestamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
        var defaultPath = Path.Combine(Environment.CurrentDirectory, $"fsequal-{format}-{timestamp}.{GetExtension(format)}");
        var path = AnsiConsole.Ask("Export path:", defaultPath);

        var filtered = filter.HasValue
            ? _result.Differences.Where(d => d.Type == filter.Value).ToList()
            : _result.Differences.ToList();

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? Environment.CurrentDirectory);

        switch (format)
        {
            case "json":
                var jsonPayload = new
                {
                    Filter = filter?.ToString() ?? "All",
                    GeneratedAtUtc = DateTimeOffset.UtcNow,
                    Differences = filtered.Select(d => new
                    {
                        Type = d.Type.ToString(),
                        Path = d.RelativePath,
                        Detail = d.Detail,
                        Left = d.Left?.FullPath,
                        Right = d.Right?.FullPath,
                    }),
                };
                await File.WriteAllTextAsync(path, JsonSerializer.Serialize(jsonPayload, new JsonSerializerOptions { WriteIndented = true }), token);
                break;
            case "csv":
                var builder = new StringBuilder();
                builder.AppendLine("Type,Path,Detail");
                foreach (var diff in filtered)
                {
                    builder.AppendLine($"\"{diff.Type}\",\"{diff.RelativePath.Replace("\"", "\"\"")}\",\"{(diff.Detail ?? string.Empty).Replace("\"", "\"\"")}\"");
                }
                await File.WriteAllTextAsync(path, builder.ToString(), token);
                break;
            case "markdown":
                var md = new StringBuilder();
                md.AppendLine("| Type | Path | Detail |");
                md.AppendLine("| --- | --- | --- |");
                foreach (var diff in filtered)
                {
                    md.AppendLine($"| {diff.Type} | {diff.RelativePath.Replace("|", "\\|")} | {(diff.Detail ?? string.Empty).Replace("|", "\\|")} |");
                }
                await File.WriteAllTextAsync(path, md.ToString(), token);
                break;
        }

        _logger.Log(VerbosityLevel.Info, $"Exported {filtered.Count} entries to {path}.");
    }

    private static string GetExtension(string format)
    {
        return format switch
        {
            "json" => "json",
            "csv" => "csv",
            "markdown" => "md",
            _ => "txt",
        };
    }
}
