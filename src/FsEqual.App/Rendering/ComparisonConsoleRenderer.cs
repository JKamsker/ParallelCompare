using System;
using System.IO;
using System.Linq;
using FsEqual.Core.Comparison;
using FsEqual.Core.Options;
using Spectre.Console;

namespace FsEqual.App.Rendering;

/// <summary>
/// Renders comparison results and watch summaries to the console.
/// </summary>
public static class ComparisonConsoleRenderer
{
    /// <summary>
    /// Renders the comparison context and summary metrics to the console.
    /// </summary>
    /// <param name="result">Comparison result to render.</param>
    public static void RenderSummary(ComparisonResult result)
    {
        AnsiConsole.Write(BuildContextPanel(result));

        var summary = result.Summary;
        var table = new Table().Title("[bold]Comparison Summary[/]");
        table.AddColumn("Metric");
        table.AddColumn("Value");

        table.AddRow("Total Files", summary.TotalFiles.ToString());
        table.AddRow("Equal", summary.EqualFiles.ToString());
        table.AddRow("Different", summary.DifferentFiles.ToString());
        table.AddRow("Left Only", summary.LeftOnlyFiles.ToString());
        table.AddRow("Right Only", summary.RightOnlyFiles.ToString());
        table.AddRow("Errors", summary.ErrorFiles.ToString());

        AnsiConsole.Write(table);
    }

    /// <summary>
    /// Writes a descriptive status line summarizing the active watch session.
    /// </summary>
    /// <param name="result">Most recent comparison result.</param>
    /// <param name="resolved">Resolved settings used for the run.</param>
    /// <param name="lastSuccessfulRun">Timestamp of the last successful comparison execution.</param>
    public static void RenderWatchStatus(ComparisonResult result, ResolvedCompareSettings resolved, DateTimeOffset lastSuccessfulRun)
    {
        string message;
        var timestamp = Markup.Escape(lastSuccessfulRun.ToLocalTime().ToString("T"));

        if (resolved.UsesBaseline && result.Baseline is { } baseline)
        {
            var manifestName = Path.GetFileName(baseline.ManifestPath);
            message = $"Watching [bold]{Markup.Escape(result.LeftPath)}[/] against baseline [bold]{Markup.Escape(manifestName)}[/] captured {baseline.CreatedAt:u}. Last successful comparison at {timestamp}.";
        }
        else
        {
            message = $"Watching [bold]{Markup.Escape(result.LeftPath)}[/] and [bold]{Markup.Escape(result.RightPath)}[/]. Last successful comparison at {timestamp}.";
        }

        AnsiConsole.MarkupLine($"[grey]{message}[/]");
    }

    /// <summary>
    /// Renders a tree view of the comparison result up to the specified depth.
    /// </summary>
    /// <param name="result">Comparison result to display.</param>
    /// <param name="maxDepth">Maximum depth to expand within the tree.</param>
    public static void RenderTree(ComparisonResult result, int maxDepth = 3)
    {
        var tree = new Tree(GetNodeLabel(result.Root));
        BuildTree(tree.AddNode, result.Root, 1, maxDepth);
        AnsiConsole.Write(tree);
    }

    private static Panel BuildContextPanel(ComparisonResult result)
    {
        var grid = new Grid();
        grid.AddColumn();
        grid.AddRow(new Markup($"[bold]Left:[/] {Markup.Escape(result.LeftPath)}"));

        if (result.Baseline is { } baseline)
        {
            grid.AddRow(new Markup($"[bold]Baseline Source:[/] {Markup.Escape(baseline.SourcePath)}"));
            grid.AddRow(new Markup($"[bold]Manifest:[/] {Markup.Escape(baseline.ManifestPath)}"));
            grid.AddRow(new Markup($"[bold]Captured:[/] {baseline.CreatedAt:u}"));

            if (!baseline.Algorithms.IsDefaultOrEmpty)
            {
                var algorithms = string.Join(", ", baseline.Algorithms.Select(a => a.ToString().ToUpperInvariant()));
                grid.AddRow(new Markup($"[bold]Algorithms:[/] {Markup.Escape(algorithms)}"));
            }
        }
        else
        {
            grid.AddRow(new Markup($"[bold]Right:[/] {Markup.Escape(result.RightPath)}"));
        }

        return new Panel(grid)
            .Border(BoxBorder.Rounded)
            .Header(" Context ");
    }

    private static void BuildTree(Func<string, TreeNode> addNode, ComparisonNode node, int depth, int maxDepth)
    {
        if (depth > maxDepth)
        {
            if (node.Children.Length > 0)
            {
                addNode("[grey]…[/]");
            }

            return;
        }

        foreach (var child in node.Children)
        {
            var childNode = addNode(GetNodeLabel(child));
            BuildTree(childNode.AddNode, child, depth + 1, maxDepth);
        }
    }

    private static string GetNodeLabel(ComparisonNode node)
    {
        var status = node.Status switch
        {
            ComparisonStatus.Equal => "[green]Equal[/]",
            ComparisonStatus.Different => "[yellow]Different[/]",
            ComparisonStatus.LeftOnly => "[blue]Left Only[/]",
            ComparisonStatus.RightOnly => "[magenta]Right Only[/]",
            ComparisonStatus.Error => "[red]Error[/]",
            _ => node.Status.ToString()
        };

        return node.NodeType == ComparisonNodeType.Directory
            ? $"{node.Name} ({status})"
            : $"{node.Name} - {status}";
    }
}
