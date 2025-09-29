using System.Text;
using FsEqual.Tool.Models;
using FsEqual.Tool.Services;
using Spectre.Console;

namespace FsEqual.Tool.Interactive;

public sealed class InteractiveExplorer
{
    private readonly ReportExporter _exporter = new();

    public async Task RunAsync(ComparisonReport report, CancellationToken cancellationToken)
    {
        var selectedStatuses = new HashSet<ComparisonStatus>(Enum.GetValues<ComparisonStatus>());

        while (!cancellationToken.IsCancellationRequested)
        {
            AnsiConsole.Clear();
            RenderHeader(report);
            RenderSummary(report, selectedStatuses);

            var choice = AnsiConsole.Prompt(new SelectionPrompt<string>
            {
                Title = "[bold]Choose an action[/]",
                PageSize = 10,
            }
            .AddChoices("Browse", "Filter", "Export", "Quit"));

            switch (choice)
            {
                case "Browse":
                    Browse(report, selectedStatuses);
                    break;
                case "Filter":
                    selectedStatuses = SelectStatuses(selectedStatuses);
                    break;
                case "Export":
                    await ExportAsync(report, cancellationToken);
                    break;
                case "Quit":
                    return;
            }
        }
    }

    private static void RenderHeader(ComparisonReport report)
    {
        var header = new FigletText("fsEqual")
        {
            Color = Color.Aqua,
        };
        AnsiConsole.Write(header);
        AnsiConsole.MarkupLine($"[bold]Left:[/] {report.LeftRoot}");
        if (!string.IsNullOrEmpty(report.RightRoot))
        {
            AnsiConsole.MarkupLine($"[bold]Right:[/] {report.RightRoot}");
        }
        AnsiConsole.MarkupLine($"Mode: {report.Mode} | Algo: {report.Algorithm} | Duration: {report.Summary.Duration}");
        AnsiConsole.WriteLine();
    }

    private static void RenderSummary(ComparisonReport report, HashSet<ComparisonStatus> filter)
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Metric");
        table.AddColumn("Value");
        table.AddRow("Total", report.Summary.TotalItems.ToString());
        table.AddRow("Equal", report.Summary.Equal.ToString());
        table.AddRow("Differences", report.Summary.Differences.ToString());
        table.AddRow("Missing Left", report.Summary.MissingLeft.ToString());
        table.AddRow("Missing Right", report.Summary.MissingRight.ToString());
        table.AddRow("Errors", report.Errors.Count.ToString());
        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        var filtered = report.Items.Where(item => filter.Contains(item.Status) && item.Status != ComparisonStatus.Equal).ToList();
        if (filtered.Count == 0)
        {
            AnsiConsole.MarkupLine("[green]No differences for the current filter.[/]");
            return;
        }

        var preview = new Table().Border(TableBorder.Rounded);
        preview.AddColumn("Status");
        preview.AddColumn("Path");
        preview.AddColumn("Reason");
        foreach (var item in filtered.Take(15))
        {
            preview.AddRow(item.Status.ToString(), item.RelativePath, item.Reason ?? string.Empty);
        }

        if (filtered.Count > 15)
        {
            preview.Caption = new TableTitle($"Showing first 15 of {filtered.Count} matching entries");
        }

        AnsiConsole.Write(preview);
        AnsiConsole.WriteLine();
    }

    private static void Browse(ComparisonReport report, HashSet<ComparisonStatus> filter)
    {
        var filtered = report.Items
            .Where(item => filter.Contains(item.Status) && item.Kind == PathKind.File)
            .OrderBy(item => item.RelativePath)
            .ToList();

        if (filtered.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No files match the current filter.[/]");
            AnsiConsole.MarkupLine("Press any key to continue...");
            Console.ReadKey(true);
            return;
        }

        var prompt = new SelectionPrompt<PathComparison>
        {
            Title = "Select a file to inspect",
            PageSize = 15,
            Converter = item => $"[{GetColor(item.Status)}]{item.Status}[/] {item.RelativePath}",
        };
        prompt.AddChoices(filtered);
        var selected = AnsiConsole.Prompt(prompt);
        ShowDetails(selected);
    }

    private static string GetColor(ComparisonStatus status) => status switch
    {
        ComparisonStatus.Equal => "green",
        ComparisonStatus.HashMismatch => "red",
        ComparisonStatus.SizeMismatch => "red",
        ComparisonStatus.MetadataMismatch => "yellow",
        ComparisonStatus.MissingLeft => "red",
        ComparisonStatus.MissingRight => "red",
        ComparisonStatus.Error => "red",
        _ => "white",
    };

    private static void ShowDetails(PathComparison comparison)
    {
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine($"[bold]{comparison.RelativePath}[/] ({comparison.Status})");
        if (comparison.Left != null)
        {
            var leftTable = new Table().Title("Left");
            leftTable.AddColumn("Property");
            leftTable.AddColumn("Value");
            leftTable.AddRow("Size", comparison.Left.Size.ToString());
            leftTable.AddRow("Modified", comparison.Left.LastWriteTimeUtc.ToString("u"));
            if (!string.IsNullOrEmpty(comparison.Left.Hash))
            {
                leftTable.AddRow("Hash", comparison.Left.Hash);
            }
            AnsiConsole.Write(leftTable);
        }

        if (comparison.Right != null)
        {
            var rightTable = new Table().Title("Right");
            rightTable.AddColumn("Property");
            rightTable.AddColumn("Value");
            rightTable.AddRow("Size", comparison.Right.Size.ToString());
            rightTable.AddRow("Modified", comparison.Right.LastWriteTimeUtc.ToString("u"));
            if (!string.IsNullOrEmpty(comparison.Right.Hash))
            {
                rightTable.AddRow("Hash", comparison.Right.Hash);
            }
            AnsiConsole.Write(rightTable);
        }

        if (!string.IsNullOrEmpty(comparison.Reason))
        {
            AnsiConsole.MarkupLine($"[yellow]{comparison.Reason}[/]");
        }

        AnsiConsole.MarkupLine("Press any key to return...");
        Console.ReadKey(true);
    }

    private static HashSet<ComparisonStatus> SelectStatuses(HashSet<ComparisonStatus> current)
    {
        var prompt = new MultiSelectionPrompt<ComparisonStatus>
        {
            Title = "Select statuses to display",
        };
        prompt.AddChoices(Enum.GetValues<ComparisonStatus>());
        foreach (var status in current)
        {
            prompt.Select(status);
        }
        var selected = AnsiConsole.Prompt(prompt);
        return new HashSet<ComparisonStatus>(selected);
    }

    private async Task ExportAsync(ComparisonReport report, CancellationToken cancellationToken)
    {
        var format = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title("Select export format")
            .AddChoices("json", "markdown", "csv", "cancel"));

        if (format == "cancel")
        {
            return;
        }

        var path = AnsiConsole.Ask<string>("Enter output path:");
        switch (format)
        {
            case "json":
                await _exporter.ExportFullAsync(report, path, cancellationToken);
                break;
            case "markdown":
                await File.WriteAllTextAsync(path, BuildMarkdown(report), cancellationToken);
                break;
            case "csv":
                await File.WriteAllTextAsync(path, BuildCsv(report), cancellationToken);
                break;
        }

        AnsiConsole.MarkupLine($"[green]Exported report to {path}[/]");
        AnsiConsole.MarkupLine("Press any key to continue...");
        Console.ReadKey(true);
    }

    private static string BuildMarkdown(ComparisonReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# fsEqual Report");
        builder.AppendLine();
        builder.AppendLine($"* Left: `{report.LeftRoot}`");
        if (!string.IsNullOrEmpty(report.RightRoot))
        {
            builder.AppendLine($"* Right: `{report.RightRoot}`");
        }
        builder.AppendLine($"* Mode: {report.Mode}");
        builder.AppendLine($"* Algorithm: {report.Algorithm}");
        builder.AppendLine($"* Duration: {report.Summary.Duration}");
        builder.AppendLine();
        builder.AppendLine("| Status | Path | Reason |");
        builder.AppendLine("| --- | --- | --- |");
        foreach (var item in report.Items.Where(i => i.Status != ComparisonStatus.Equal))
        {
            builder.AppendLine($"| {item.Status} | {item.RelativePath} | {item.Reason} |");
        }

        return builder.ToString();
    }

    private static string BuildCsv(ComparisonReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("status,path,reason");
        foreach (var item in report.Items.Where(i => i.Status != ComparisonStatus.Equal))
        {
            builder.AppendLine($"\"{item.Status}\",\"{item.RelativePath.Replace("\"", "''")}\",\"{(item.Reason ?? string.Empty).Replace("\"", "''")}\"");
        }

        return builder.ToString();
    }
}
