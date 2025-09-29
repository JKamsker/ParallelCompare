using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Linq;
using FsEqual.Core;
using FsEqual.Reporting;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace FsEqual.Interactive;

public sealed class InteractiveSession
{
    private readonly IAnsiConsole _console;
    private ComparisonOptions _options;
    private ComparisonResult _result;
    private NodeView _root = null!;
    private readonly HashSet<string> _collapsed;
    private int _selectedIndex;
    private FilterMode _filterMode = FilterMode.All;
    private readonly Dictionary<string, DifferenceRecord> _differenceLookup;

    public InteractiveSession(ComparisonOptions options, ComparisonResult result, IAnsiConsole console)
    {
        _options = options;
        _result = result;
        _console = console;
        _collapsed = new HashSet<string>(_options.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
        _differenceLookup = result.Differences.GroupBy(d => d.Path, _options.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), _options.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
        BuildTree();
    }

    public void Run()
    {
        var cursorSupported = OperatingSystem.IsWindows();
        var previousCursor = cursorSupported ? Console.CursorVisible : default;
        if (cursorSupported)
        {
            Console.CursorVisible = false;
        }
        try
        {
            bool exit = false;
            while (!exit)
            {
                Render();
                var key = Console.ReadKey(true);
                switch (key.Key)
                {
                    case ConsoleKey.Escape:
                    case ConsoleKey.Q:
                        exit = true;
                        break;
                    case ConsoleKey.UpArrow:
                        MoveSelection(-1);
                        break;
                    case ConsoleKey.DownArrow:
                        MoveSelection(1);
                        break;
                    case ConsoleKey.LeftArrow:
                        CollapseOrParent();
                        break;
                    case ConsoleKey.RightArrow:
                        ExpandOrChild();
                        break;
                    case ConsoleKey.Enter:
                        ToggleExpand();
                        break;
                    case ConsoleKey.F:
                        CycleFilter();
                        break;
                    case ConsoleKey.E:
                        ExportPrompt();
                        break;
                    case ConsoleKey.R:
                        Refresh();
                        break;
                    case ConsoleKey.A:
                        ChangeAlgorithm();
                        break;
                    case ConsoleKey.L:
                        CycleVerbosity();
                        break;
                    case ConsoleKey.Oem2:
                        ShowHelp();
                        break;
                }
            }
        }
        finally
        {
            if (cursorSupported)
            {
                Console.CursorVisible = previousCursor;
            }
            _console.Clear();
        }
    }

    private void Render()
    {
        var visible = GetVisibleNodes();
        if (_selectedIndex >= visible.Count)
        {
            _selectedIndex = Math.Max(0, visible.Count - 1);
        }

        _console.Clear();

        var header = new Panel(new Markup($"[bold]{Markup.Escape(_options.LeftRoot)}[/]\nvs\n[bold]{Markup.Escape(_options.RightRoot)}[/]\nMode: {_options.Mode}, Algo: {_options.HashAlgorithm}, Threads: {_options.Threads?.ToString() ?? "auto"}, Duration: {_result.Elapsed:g}"));
        var treePanel = new Panel(new Markup(RenderTree(visible)))
        {
            Header = new PanelHeader("Structure"),
            Border = BoxBorder.Square,
            Padding = new Padding(1)
        };

        var detailsPanel = new Panel(RenderDetails(visible.Count == 0 ? null : visible[_selectedIndex].View))
        {
            Header = new PanelHeader("Details"),
            Border = BoxBorder.Square,
            Padding = new Padding(1)
        };

        var columns = new Columns(treePanel, detailsPanel) { Expand = true };
        _console.Write(columns);
        _console.WriteLine();

        _console.MarkupLine($"Filter: [yellow]{_filterMode}[/] | Differences: {_result.Summary.DifferentFiles}, Missing: {_result.Summary.MissingLeft + _result.Summary.MissingRight}, Errors: {_result.Summary.Errors}");
        _console.MarkupLine("[grey]Keys: ↑/↓ navigate, ←/→ expand/collapse, Enter toggle, F filter, A algorithm, R refresh, E export, L cycle verbosity, Q quit, ? help[/]");
    }

    private string RenderTree(List<VisibleNode> nodes)
    {
        if (nodes.Count == 0)
        {
            return "[grey](no nodes)[/]";
        }

        var builder = new StringBuilder();
        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            var indent = new string(' ', node.Depth * 2);
            var toggle = node.View.Node.IsDirectory ? (_collapsed.Contains(node.View.Node.RelativePath) ? "▸" : "▾") : " ";
            var glyph = GetGlyph(node.View);
            var label = FormatLabel(node.View);
            var line = $"{indent}{toggle} {glyph} {label}";
            if (node.View.Node.IsDirectory)
            {
                line += $" [grey][{node.View.Node.EqualCount}/{node.View.Node.DiffCount}/{node.View.Node.MissingCount}/{node.View.Node.ErrorCount}][/]";
            }

            if (i == _selectedIndex)
            {
                line = $"[reverse]{line}[/]";
            }

            builder.AppendLine(line);
        }

        return builder.ToString();
    }

    private IRenderable RenderDetails(NodeView? view)
    {
        if (view is null)
        {
            return new Markup("[grey]No selection[/]");
        }

        var rows = new List<IRenderable>
        {
            new Markup($"[bold]{Markup.Escape(view.Node.RelativePath.Length == 0 ? "." : view.Node.RelativePath)}[/]"),
            new Markup($"Status: {DescribeStatus(view)}")
        };

        if (view.Node.IsDirectory)
        {
            var table = new Table().Border(TableBorder.Rounded).AddColumn("Metric").AddColumn("Value");
            table.AddRow("Equal", view.Node.EqualCount.ToString());
            table.AddRow("Diff", view.Node.DiffCount.ToString());
            table.AddRow("Missing", view.Node.MissingCount.ToString());
            table.AddRow("Errors", view.Node.ErrorCount.ToString());
            rows.Add(table);
        }
        else
        {
            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("Side");
            table.AddColumn("Size");
            table.AddColumn("Modified");
            table.AddColumn("Hash");

            var left = view.Node.LeftMetadata;
            var right = view.Node.RightMetadata;
            table.AddRow("Left", FormatSize(left?.Size), FormatTimestamp(left?.LastWriteTimeUtc), left?.Hash ?? string.Empty);
            table.AddRow("Right", FormatSize(right?.Size), FormatTimestamp(right?.LastWriteTimeUtc), right?.Hash ?? string.Empty);
            rows.Add(table);

            if (_differenceLookup.TryGetValue(view.Node.RelativePath, out var diff) && !string.IsNullOrEmpty(diff.Reason))
            {
                rows.Add(new Markup($"Reason: {Markup.Escape(diff.Reason)}"));
            }
        }

        return new Rows(rows);
    }

    private void MoveSelection(int delta)
    {
        var nodes = GetVisibleNodes();
        if (nodes.Count == 0)
        {
            _selectedIndex = 0;
            return;
        }

        _selectedIndex = Math.Clamp(_selectedIndex + delta, 0, nodes.Count - 1);
    }

    private void CollapseOrParent()
    {
        var nodes = GetVisibleNodes();
        if (nodes.Count == 0)
        {
            return;
        }

        var current = nodes[_selectedIndex].View;
        if (current.Node.IsDirectory && !_collapsed.Contains(current.Node.RelativePath))
        {
            if (current.Node.RelativePath.Length > 0)
            {
                _collapsed.Add(current.Node.RelativePath);
            }
        }
        else if (current.Parent is not null)
        {
            var parentIndex = nodes.FindIndex(n => n.View == current.Parent);
            if (parentIndex >= 0)
            {
                _selectedIndex = parentIndex;
            }
        }
    }

    private void ExpandOrChild()
    {
        var nodes = GetVisibleNodes();
        if (nodes.Count == 0)
        {
            return;
        }

        var current = nodes[_selectedIndex].View;
        if (current.Node.IsDirectory)
        {
            if (_collapsed.Remove(current.Node.RelativePath))
            {
                return;
            }

            if (current.Children.Count > 0)
            {
                var childIndex = nodes.FindIndex(n => n.View == current.Children[0]);
                if (childIndex >= 0)
                {
                    _selectedIndex = childIndex;
                }
            }
        }
    }

    private void ToggleExpand()
    {
        var nodes = GetVisibleNodes();
        if (nodes.Count == 0)
        {
            return;
        }

        var current = nodes[_selectedIndex].View;
        if (!current.Node.IsDirectory || current.Node.RelativePath.Length == 0)
        {
            return;
        }

        if (_collapsed.Contains(current.Node.RelativePath))
        {
            _collapsed.Remove(current.Node.RelativePath);
        }
        else
        {
            _collapsed.Add(current.Node.RelativePath);
        }
    }

    private void CycleFilter()
    {
        _filterMode = _filterMode switch
        {
            FilterMode.All => FilterMode.Differences,
            FilterMode.Differences => FilterMode.Errors,
            _ => FilterMode.All
        };
        _selectedIndex = 0;
    }

    private void ExportPrompt()
    {
        var format = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title("Export format")
            .AddChoices("json", "csv", "markdown"));
        var path = AnsiConsole.Prompt(new TextPrompt<string>("Enter export path:")
            .PromptStyle("green")
            .Validate(s => string.IsNullOrWhiteSpace(s) ? ValidationResult.Error("Path cannot be empty") : ValidationResult.Success()));

        var exporter = new ReportExporter();
        exporter.ExportAsync(_result, path, format, _console).GetAwaiter().GetResult();
        AnsiConsole.MarkupLine("[green]Export complete.[/]");
        AnsiConsole.MarkupLine("Press any key to continue...");
        Console.ReadKey(true);
    }

    private void Refresh()
    {
        var service = new ComparisonService(_options, _console);
        _result = service.ExecuteAsync(CancellationToken.None).GetAwaiter().GetResult();
        _differenceLookup.Clear();
        foreach (var diff in _result.Differences)
        {
            _differenceLookup[diff.Path] = diff;
        }
        BuildTree();
    }

    private void ChangeAlgorithm()
    {
        var choices = new[] { "crc32", "md5", "sha256", "xxh64" };
        var current = _options.HashAlgorithm.ToString().ToLowerInvariant();
        var prompt = new SelectionPrompt<string>()
            .Title("Select hash algorithm")
            .AddChoices(choices)
            .HighlightStyle(new Style(Color.Yellow));
        var selected = AnsiConsole.Prompt(prompt);
        var newAlgorithm = selected switch
        {
            "crc32" => HashAlgorithmKind.Crc32,
            "md5" => HashAlgorithmKind.Md5,
            "sha256" => HashAlgorithmKind.Sha256,
            "xxh64" => HashAlgorithmKind.Xxh64,
            _ => _options.HashAlgorithm
        };

        _options = _options with { Mode = ComparisonMode.Hash, HashAlgorithm = newAlgorithm };
        Refresh();
    }

    private void CycleVerbosity()
    {
        AnsiConsole.MarkupLine("[grey]Verbosity cycling not implemented yet.[/]");
        AnsiConsole.MarkupLine("Press any key to continue...");
        Console.ReadKey(true);
    }

    private void ShowHelp()
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Key");
        table.AddColumn("Description");
        table.AddRow("↑/↓", "Move selection");
        table.AddRow("←/→", "Collapse/expand navigation");
        table.AddRow("Enter", "Toggle current directory");
        table.AddRow("F", "Cycle filters (All → Differences → Errors)");
        table.AddRow("A", "Re-run comparison with a different hash algorithm");
        table.AddRow("R", "Refresh comparison with current settings");
        table.AddRow("E", "Export current differences (json/csv/markdown)");
        table.AddRow("L", "Reserved for verbosity toggle");
        table.AddRow("Q", "Quit interactive mode");
        _console.Clear();
        _console.Write(table);
        _console.MarkupLine("Press any key to return.");
        Console.ReadKey(true);
    }

    private string FormatLabel(NodeView node)
    {
        var name = node.Node.RelativePath.Length == 0 ? "." : node.Node.Name;
        var color = GetColor(node);
        return $"[{color}]{Markup.Escape(name)}[/]";
    }

    private string GetGlyph(NodeView node)
    {
        if (node.Node.IsDirectory)
        {
            if (node.Node.ErrorCount > 0)
            {
                return "[red]?[/]";
            }
            if (node.Node.MissingCount > 0 || node.Node.DiffCount > 0)
            {
                return "[yellow]![/]";
            }
            return "[green]✓[/]";
        }

        return node.Node.Status switch
        {
            null => "[green]✓[/]",
            DifferenceType.MissingLeft => "[yellow]←[/]",
            DifferenceType.MissingRight => "[yellow]→[/]",
            DifferenceType.HashMismatch or DifferenceType.SizeMismatch or DifferenceType.TimestampMismatch => "[yellow]![/]",
            DifferenceType.Error => "[red]?[/]",
            _ => "[yellow]![/]"
        };
    }

    private string GetColor(NodeView node)
    {
        if (node.Node.IsDirectory)
        {
            if (node.Node.ErrorCount > 0)
            {
                return "red";
            }
            if (node.Node.MissingCount > 0 || node.Node.DiffCount > 0)
            {
                return "yellow";
            }
            return "green";
        }

        return node.Node.Status switch
        {
            null => "green",
            DifferenceType.Error => "red",
            DifferenceType.MissingLeft or DifferenceType.MissingRight => "yellow",
            _ => "yellow"
        };
    }

    private string DescribeStatus(NodeView node)
    {
        if (node.Node.IsDirectory)
        {
            if (node.Node.ErrorCount > 0)
            {
                return "Contains errors";
            }
            if (node.Node.MissingCount > 0 || node.Node.DiffCount > 0)
            {
                return "Contains differences";
            }
            return "All children equal";
        }

        return node.Node.Status switch
        {
            null => "Equal",
            DifferenceType.MissingLeft => "Missing on left",
            DifferenceType.MissingRight => "Missing on right",
            DifferenceType.SizeMismatch => "Size mismatch",
            DifferenceType.HashMismatch => "Hash mismatch",
            DifferenceType.TimestampMismatch => "Timestamp mismatch",
            DifferenceType.Error => "Error computing",
            DifferenceType.TypeMismatch => "Type mismatch",
            _ => node.Node.Status?.ToString() ?? "Unknown"
        };
    }

    private static string FormatSize(long? value)
    {
        return value is null ? string.Empty : $"{value:N0} bytes";
    }

    private static string FormatTimestamp(DateTime? value)
    {
        return value is null ? string.Empty : value.Value.ToString("u");
    }

    private List<VisibleNode> GetVisibleNodes()
    {
        var list = new List<VisibleNode>();
        AppendVisible(_root, 0, list);
        return list;
    }

    private void AppendVisible(NodeView node, int depth, List<VisibleNode> list)
    {
        if (!ShouldInclude(node))
        {
            return;
        }

        list.Add(new VisibleNode(node, depth));
        if (node.Node.IsDirectory && !_collapsed.Contains(node.Node.RelativePath))
        {
            foreach (var child in node.Children)
            {
                AppendVisible(child, depth + 1, list);
            }
        }
    }

    private bool ShouldInclude(NodeView node)
    {
        if (_filterMode == FilterMode.All)
        {
            return true;
        }

        bool Matches(NodeView v) => _filterMode switch
        {
            FilterMode.Differences => v.Node.DiffCount > 0 || v.Node.MissingCount > 0,
            FilterMode.Errors => v.Node.ErrorCount > 0,
            _ => true
        };

        if (Matches(node))
        {
            return true;
        }

        if (node.Node.IsDirectory)
        {
            return node.Children.Any(ShouldInclude);
        }

        return false;
    }

    private void BuildTree()
    {
        string? selectedPath = null;
        if (_root is not null)
        {
            var previous = GetVisibleNodes();
            if (previous.Count > 0 && _selectedIndex < previous.Count)
            {
                selectedPath = previous[_selectedIndex].View.Node.RelativePath;
            }
        }

        _root = BuildNode(_result.RootNode, null);

        var visible = GetVisibleNodes();
        if (!string.IsNullOrEmpty(selectedPath))
        {
            var comparer = _options.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            var index = visible.FindIndex(n => string.Equals(n.View.Node.RelativePath, selectedPath, comparer));
            _selectedIndex = index >= 0 ? index : 0;
        }
        else
        {
            _selectedIndex = 0;
        }
    }

    private NodeView BuildNode(ComparisonNode node, NodeView? parent)
    {
        var view = new NodeView(node, parent);
        foreach (var child in node.Children)
        {
            view.Children.Add(BuildNode(child, view));
        }
        return view;
    }

    private sealed class NodeView
    {
        public NodeView(ComparisonNode node, NodeView? parent)
        {
            Node = node;
            Parent = parent;
        }

        public ComparisonNode Node { get; }
        public NodeView? Parent { get; }
        public List<NodeView> Children { get; } = new();
    }

    private sealed record VisibleNode(NodeView View, int Depth);

    private enum FilterMode
    {
        All,
        Differences,
        Errors
    }
}
