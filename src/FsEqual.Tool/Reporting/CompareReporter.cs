using System.Text.Json;
using FsEqual.Tool.Commands;
using FsEqual.Tool.Comparison;
using Spectre.Console;

namespace FsEqual.Tool.Reporting;

internal sealed class CompareReporter
{
    private readonly ComparisonOptions _options;
    private readonly CompareSettings _settings;

    public CompareReporter(ComparisonOptions options, CompareSettings settings)
    {
        _options = options;
        _settings = settings;
    }

    public void Render(ComparisonResult result)
    {
        RenderHeader(result);
        RenderSummary(result);
        RenderDifferences(result);
        RenderIssues(result);
        WriteExports(result);
    }

    private void RenderHeader(ComparisonResult result)
    {
        var table = new Table().Border(TableBorder.None);
        table.AddColumn(new TableColumn("Key").NoWrap());
        table.AddColumn(new TableColumn("Value"));

        table.AddRow("Left", _options.Left);
        if (!string.IsNullOrWhiteSpace(_options.Right))
        {
            table.AddRow("Right", _options.Right!);
        }
        if (!string.IsNullOrWhiteSpace(_options.BaselinePath))
        {
            table.AddRow("Baseline", _options.BaselinePath!);
        }

        table.AddRow("Mode", _options.Mode.ToString());
        table.AddRow("Algorithm", _options.Algorithm.ToString());
        table.AddRow("Workers", _options.MaxDegreeOfParallelism.ToString());
        table.AddRow("Duration", result.Duration.ToString("g"));

        AnsiConsole.Write(new Panel(table)
            .Header("fsEqual Compare")
            .Border(BoxBorder.Rounded)
            .Expand());
    }

    private void RenderSummary(ComparisonResult result)
    {
        var summary = result.Summary;
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

        var status = result.AreEqual ? "[green]Equal[/]" : summary.Errors > 0 ? "[red]Errors[/]" : "[yellow]Differences[/]";
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"Result: {status}");
        AnsiConsole.Write(table);
    }

    private void RenderDifferences(ComparisonResult result)
    {
        if (_options.Verbosity < VerbosityLevel.Info)
        {
            return;
        }

        var diffTable = new Table().Border(TableBorder.Rounded).Expand();
        diffTable.AddColumn("Kind");
        diffTable.AddColumn("Status");
        diffTable.AddColumn("Path");
        diffTable.AddColumn("Detail");

        var maxRows = _options.Verbosity >= VerbosityLevel.Debug ? int.MaxValue : 50;
        var count = 0;
        foreach (var entry in result.Differences.Concat(result.DirectoryDifferences).Concat(result.BaselineDifferences))
        {
            diffTable.AddRow(entry.Kind.ToString(), entry.Status.ToString(), entry.Path, entry.Detail ?? string.Empty);
            count++;
            if (count >= maxRows)
            {
                break;
            }
        }

        if (count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold]Differences[/]");
            AnsiConsole.Write(diffTable);

            var totalDiffs = result.Differences.Count + result.DirectoryDifferences.Count + result.BaselineDifferences.Count;
            if (count < totalDiffs)
            {
                AnsiConsole.MarkupLine($"Showing {count} of {totalDiffs} differences. Use --verbosity debug for full list.");
            }
        }
        else if (_options.Verbosity >= VerbosityLevel.Debug)
        {
            AnsiConsole.MarkupLine("No differences detected.");
        }
    }

    private void RenderIssues(ComparisonResult result)
    {
        if (result.Issues.Count == 0 || _options.Verbosity < VerbosityLevel.Warn)
        {
            return;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold red]Issues[/]");
        foreach (var issue in result.Issues)
        {
            AnsiConsole.MarkupLine($"- {issue.Message}");
            if (_options.Verbosity >= VerbosityLevel.Trace && issue.Exception is not null)
            {
                AnsiConsole.WriteException(issue.Exception, ExceptionFormats.ShortenEverything);
            }
        }
    }

    private void WriteExports(ComparisonResult result)
    {
        if (string.IsNullOrWhiteSpace(_settings.JsonOutput) && string.IsNullOrWhiteSpace(_settings.SummaryOutput))
        {
            return;
        }

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        if (!string.IsNullOrWhiteSpace(_settings.JsonOutput))
        {
            var path = ResolvePath(_settings.JsonOutput!);
            var payload = JsonSerializer.Serialize(result, options);
            File.WriteAllText(path, payload);
            AnsiConsole.MarkupLine($"[green]Wrote detailed report to[/] {path}");
        }

        if (!string.IsNullOrWhiteSpace(_settings.SummaryOutput))
        {
            var path = ResolvePath(_settings.SummaryOutput!);
            var payload = JsonSerializer.Serialize(result.Summary, options);
            File.WriteAllText(path, payload);
            AnsiConsole.MarkupLine($"[green]Wrote summary report to[/] {path}");
        }
    }

    private static string ResolvePath(string path)
    {
        return Path.GetFullPath(path);
    }
}
