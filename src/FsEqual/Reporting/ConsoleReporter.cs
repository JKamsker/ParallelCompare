using FsEqual.Core;
using Spectre.Console;
using System.Linq;

namespace FsEqual.Reporting;

public sealed class ConsoleReporter
{
    private readonly IAnsiConsole _console;
    private readonly VerbosityLevel _verbosity;

    public ConsoleReporter(IAnsiConsole console, VerbosityLevel verbosity)
    {
        _console = console;
        _verbosity = verbosity;
    }

    public void Render(ComparisonResult result, ComparisonOptions options)
    {
        var rule = new Rule($"[bold cyan]FsEqual Compare[/] [grey]{options.LeftRoot}[/] ⇆ [grey]{options.RightRoot}[/]");
        rule.Justify(Justify.Left);
        _console.Write(rule);
        _console.MarkupLine($"Mode: [yellow]{options.Mode}[/], Algo: [yellow]{options.HashAlgorithm}[/], Threads: [yellow]{options.Threads?.ToString() ?? "auto"}[/], Duration: [yellow]{result.Elapsed:g}[/]");
        _console.WriteLine();

        var summaryTable = new Table().Border(TableBorder.Rounded).Title("Summary");
        summaryTable.AddColumn("Metric");
        summaryTable.AddColumn("Value");
        summaryTable.AddRow("Total files", result.Summary.TotalFiles.ToString());
        summaryTable.AddRow("Total directories", result.Summary.TotalDirectories.ToString());
        summaryTable.AddRow("Equal files", result.Summary.EqualFiles.ToString());
        summaryTable.AddRow("Different files", result.Summary.DifferentFiles.ToString());
        summaryTable.AddRow("Missing left", result.Summary.MissingLeft.ToString());
        summaryTable.AddRow("Missing right", result.Summary.MissingRight.ToString());
        summaryTable.AddRow("Errors", result.Summary.Errors.ToString());
        _console.Write(summaryTable);

        if (result.Differences.Length > 0)
        {
            _console.WriteLine();
            var diffTable = new Table().Border(TableBorder.MinimalHeavyHead);
            diffTable.AddColumn("Type");
            diffTable.AddColumn("Path");
            diffTable.AddColumn("Size (L/R)");
            diffTable.AddColumn("Algo");
            diffTable.AddColumn("Reason");

            var maxRows = _verbosity switch
            {
                VerbosityLevel.Trace => int.MaxValue,
                VerbosityLevel.Debug => 250,
                VerbosityLevel.Info => 100,
                VerbosityLevel.Warn => 50,
                _ => 25
            };

            foreach (var diff in result.Differences.Take(maxRows))
            {
                diffTable.AddRow(
                    diff.Type.ToString(),
                    diff.Path,
                    FormatSize(diff.LeftSize, diff.RightSize),
                    diff.Algorithm ?? string.Empty,
                    diff.Reason ?? string.Empty);
            }

            _console.Write(diffTable);

            if (result.Differences.Length > maxRows)
            {
                _console.MarkupLine($"[grey]{result.Differences.Length - maxRows} more differences omitted. Re-run with --verbosity debug for more.[/]");
            }
        }
        else
        {
            _console.MarkupLine("[green]No differences detected.[/]");
        }

        if (result.Errors.Length > 0)
        {
            _console.WriteLine();
            _console.MarkupLine("[red]Errors:[/]");
            foreach (var error in result.Errors.Take(20))
            {
                _console.MarkupLineInterpolated($"  [red]-[/] {error}");
            }
            if (result.Errors.Length > 20)
            {
                _console.MarkupLine($"[grey]{result.Errors.Length - 20} additional errors not shown.[/]");
            }
        }
    }

    private static string FormatSize(long? left, long? right)
    {
        if (left is null && right is null)
        {
            return string.Empty;
        }

        string Format(long? value) => value is null ? "-" : $"{value.Value:N0}";
        return $"{Format(left)} / {Format(right)}";
    }
}
