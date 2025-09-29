using System;
using ParallelCompare.Core.Comparison;
using Spectre.Console;

namespace ParallelCompare.App.Rendering;

public static class ComparisonConsoleRenderer
{
    public static void RenderSummary(ComparisonResult result)
    {
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

    public static void RenderTree(ComparisonResult result, int maxDepth = 3)
    {
        var tree = new Tree(GetNodeLabel(result.Root));
        BuildTree(tree.AddNode, result.Root, 1, maxDepth);
        AnsiConsole.Write(tree);
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
