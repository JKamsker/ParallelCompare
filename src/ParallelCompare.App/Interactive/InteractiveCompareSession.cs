using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ParallelCompare.App.Services;
using ParallelCompare.Core.Comparison;
using ParallelCompare.Core.Options;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace ParallelCompare.App.Interactive;

/// <summary>
/// Provides the interactive comparison experience including navigation, filtering, and exports.
/// </summary>
public sealed class InteractiveCompareSession
{
    private readonly ComparisonOrchestrator _orchestrator;
    private readonly DiffToolLauncher _diffLauncher;
    private readonly InteractiveExportService _exportService = new();

    private ComparisonResult _result = null!;
    private CompareSettingsInput _input = null!;
    private ResolvedCompareSettings _resolved = null!;
    private InteractiveTheme _theme = InteractiveTheme.Dark;
    private InteractiveFilter _filter = InteractiveFilter.All;
    private InteractiveVerbosity _verbosity = InteractiveVerbosity.Info;
    private readonly List<TreeEntry> _visibleEntries = new();
    private TreeEntry? _rootEntry;
    private int _selectedIndex;
    private bool _showHelp;
    private bool _paused;
    private string? _statusMessage;
    private DateTimeOffset _lastRunAt;
    private CancellationToken _cancellationToken;
    private readonly List<HashAlgorithmType> _algorithmTypes = new();
    private readonly List<string> _algorithmNames = new();
    private int _activeAlgorithmIndex;
    private readonly Channel<Func<Task>> _actionQueue = Channel.CreateUnbounded<Func<Task>>(new UnboundedChannelOptions
    {
        AllowSynchronousContinuations = true,
        SingleReader = true,
        SingleWriter = false
    });

    /// <summary>
    /// Initializes a new instance of the <see cref="InteractiveCompareSession"/> class.
    /// </summary>
    /// <param name="orchestrator">Orchestrator used to rerun comparisons and exports.</param>
    /// <param name="diffLauncher">Launcher used to open external diff tools.</param>
    public InteractiveCompareSession(ComparisonOrchestrator orchestrator, DiffToolLauncher diffLauncher)
    {
        _orchestrator = orchestrator;
        _diffLauncher = diffLauncher;
    }

    /// <summary>
    /// Runs the interactive session using the provided comparison result and settings.
    /// </summary>
    /// <param name="result">Initial comparison result to render.</param>
    /// <param name="input">Command input used to rerun comparisons.</param>
    /// <param name="resolved">Resolved settings that guide reruns.</param>
    /// <param name="cancellationToken">Token used to cancel the session.</param>
    /// <param name="initialStatusMessage">Optional status message shown at startup.</param>
    public async Task RunAsync(
        ComparisonResult result,
        CompareSettingsInput input,
        ResolvedCompareSettings resolved,
        CancellationToken cancellationToken,
        string? initialStatusMessage = null)
    {
        _result = result;
        _input = input with { EnableInteractive = true };
        _resolved = resolved;
        _cancellationToken = cancellationToken;
        _theme = InteractiveTheme.Parse(resolved.InteractiveTheme);
        _filter = InteractiveFilterExtensions.Parse(resolved.InteractiveFilter);
        _verbosity = InteractiveVerbosityExtensions.Parse(resolved.InteractiveVerbosity);
        _showHelp = false;
        _paused = false;
        _statusMessage = initialStatusMessage;
        _lastRunAt = DateTimeOffset.UtcNow;

        UpdateAlgorithmList(_input.Algorithm);
        BuildTree();

        var cursorManaged = false;
        bool previousCursor = false;
        try
        {
#pragma warning disable CA1416
            previousCursor = Console.CursorVisible;
            Console.CursorVisible = false;
#pragma warning restore CA1416
            cursorManaged = true;
        }
        catch (PlatformNotSupportedException)
        {
            cursorManaged = false;
        }

        try
        {
            Render();

            while (true)
            {
                if (_cancellationToken.IsCancellationRequested)
                {
                    SetStatus("Cancellation requested.");
                    Render();
                    break;
                }

                if (await DrainPendingActionsAsync())
                {
                    continue;
                }

                if (TryReadKey(out var key))
                {
                    if (await HandleKeyAsync(key))
                    {
                        break;
                    }

                    Render();
                    continue;
                }

                await WaitForNextSignalAsync();
            }
        }
        catch (OperationCanceledException)
        {
            SetStatus("Cancellation requested.");
            Render();
        }
        finally
        {
            if (cursorManaged)
            {
#pragma warning disable CA1416
                Console.CursorVisible = previousCursor;
#pragma warning restore CA1416
            }

            _actionQueue.Writer.TryComplete();
        }
    }

    /// <summary>
    /// Queues a comparison refresh when the watch command detects file system changes.
    /// </summary>
    /// <param name="refreshedMessage">Status message displayed when the refresh succeeds.</param>
    /// <param name="pausedMessage">Status message displayed when refresh is paused.</param>
    /// <returns><c>true</c> if the refresh was queued; otherwise, <c>false</c>.</returns>
    public async ValueTask<bool> QueueWatchRefreshAsync(string refreshedMessage, string pausedMessage)
    {
        try
        {
            await _actionQueue.Writer.WriteAsync(async () =>
            {
                if (_paused)
                {
                    SetStatus(pausedMessage);
                    return;
                }

                await ReRunComparisonAsync(static input => input);
                SetStatus(refreshedMessage);
            });

            return true;
        }
        catch (ChannelClosedException)
        {
            return false;
        }
    }

    private async Task<bool> HandleKeyAsync(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.Q:
            case ConsoleKey.Escape:
                return true;
            case ConsoleKey.UpArrow:
                MoveSelection(-1);
                break;
            case ConsoleKey.DownArrow:
                MoveSelection(1);
                break;
            case ConsoleKey.LeftArrow:
                HandleCollapse();
                break;
            case ConsoleKey.RightArrow:
                HandleExpand();
                break;
            case ConsoleKey.PageUp:
                MoveSelection(-10);
                break;
            case ConsoleKey.PageDown:
                MoveSelection(10);
                break;
            case ConsoleKey.Home:
                MoveToIndex(0);
                break;
            case ConsoleKey.End:
                MoveToIndex(_visibleEntries.Count - 1);
                break;
            case ConsoleKey.Enter:
            case ConsoleKey.Spacebar:
                ToggleExpansion();
                break;
            case ConsoleKey.F:
                await HandleFilterAsync();
                break;
            case ConsoleKey.A:
                await HandleAlgorithmAsync();
                break;
            case ConsoleKey.R:
                await HandleRerunAsync();
                break;
            case ConsoleKey.E:
                await HandleExportAsync();
                break;
            case ConsoleKey.D:
                HandleDiff();
                break;
            case ConsoleKey.P:
                TogglePause();
                break;
            case ConsoleKey.L:
                CycleVerbosity();
                break;
            default:
                if (key.KeyChar == '?')
                {
                    _showHelp = !_showHelp;
                    SetStatus(_showHelp ? "Help shown." : "Help hidden.");
                }

                break;
        }

        return false;
    }

    private async Task<bool> DrainPendingActionsAsync()
    {
        var executed = false;

        while (_actionQueue.Reader.TryRead(out var action))
        {
            executed = true;
            await action();
        }

        if (executed)
        {
            Render();
        }

        return executed;
    }

    private async Task WaitForNextSignalAsync()
    {
        var waitTask = _actionQueue.Reader.WaitToReadAsync(_cancellationToken).AsTask();
        var delayTask = Task.Delay(100, _cancellationToken);

        var completed = await Task.WhenAny(waitTask, delayTask);
        await completed;
    }

    private static bool TryReadKey(out ConsoleKeyInfo key)
    {
        key = default;

        try
        {
            if (!Console.KeyAvailable)
            {
                return false;
            }

            key = Console.ReadKey(intercept: true);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private void Render()
    {
        AnsiConsole.Clear();

        var layout = new Layout("root")
            .SplitRows(
                new Layout("header").Size(7),
                new Layout("body"),
                new Layout("footer").Size(_showHelp ? 8 : 4));

        layout["header"].Update(RenderHeader());

        var body = new Layout("body-root")
            .SplitColumns(
                new Layout("tree").Ratio(2),
                new Layout("detail").Ratio(3));

        body["tree"].Update(RenderTree());
        body["detail"].Update(RenderDetails());
        layout["body"].Update(body);

        layout["footer"].Update(RenderFooter());

        AnsiConsole.Write(layout);
    }

    private IRenderable RenderHeader()
    {
        var summary = _result.Summary;
        var activeAlgorithm = GetActiveAlgorithmName();

        var grid = new Grid();
        grid.AddColumn(new GridColumn().Width(Math.Max(20, Math.Min(AnsiConsole.Profile.Width / 2, 60))));
        grid.AddColumn();

        var rightLabel = _result.Baseline is { } baseline
            ? $"[bold]{Markup.Escape(baseline.SourcePath)}[/] (baseline)"
            : $"[bold]{Markup.Escape(_result.RightPath)}[/]";

        grid.AddRow(
            new Markup($"[bold]{Markup.Escape(_result.LeftPath)}[/]"),
            new Markup(rightLabel));

        if (_result.Baseline is { } baselineInfo)
        {
            var algorithms = baselineInfo.Algorithms.IsDefaultOrEmpty
                ? "-"
                : string.Join(", ", baselineInfo.Algorithms.Select(a => a.ToString().ToUpperInvariant()));

            grid.AddRow(
                new Markup($"Manifest: {Markup.Escape(baselineInfo.ManifestPath)}"),
                new Markup($"Captured: {baselineInfo.CreatedAt:u}  Algorithms: {Markup.Escape(algorithms)}"));
        }

        grid.AddRow(
            new Markup($"Mode: [ {_theme.Accent}]{_resolved.Mode}[/]  Algorithm: [ {_theme.Accent}]{activeAlgorithm}[/]  Threads: {FormatThreads()}"),
            new Markup($"Filter: [ {_theme.Accent}]{_filter.ToDisplayName()}[/]  Verbosity: [ {_theme.Accent}]{_verbosity.ToDisplayName()}[/]  {( _paused ? "[yellow]Paused[/]" : "[green]Active[/]" )}"));

        grid.AddRow(
            new Markup($"Total: {summary.TotalFiles}  [ {_theme.Equal}]{summary.EqualFiles} equal[/]  [ {_theme.Different}]{summary.DifferentFiles} diff[/]  [ {_theme.LeftOnly}]{summary.LeftOnlyFiles} left[/]  [ {_theme.RightOnly}]{summary.RightOnlyFiles} right[/]  [ {_theme.Error}]{summary.ErrorFiles} error[/]"),
            new Markup($"Updated: {_lastRunAt:HH:mm:ss}  Diff tool: {FormatDiffTool()}"));

        return new Panel(grid)
            .Border(BoxBorder.Rounded)
            .Header(" Comparison ");
    }

    private IRenderable RenderTree()
    {
        var table = new Table
        {
            Border = TableBorder.None
        };

        table.AddColumn(new TableColumn(new Markup("[bold]Tree[/]")) { NoWrap = true });

        foreach (var entry in _visibleEntries)
        {
            table.AddRow(new Markup(BuildTreeRow(entry)));
        }

        return new Panel(table)
            .Border(BoxBorder.Rounded)
            .Header($" Tree ({_visibleEntries.Count}) ");
    }

    private IRenderable RenderDetails()
    {
        var entry = GetSelectedEntry();
        return entry.Node.NodeType == ComparisonNodeType.Directory
            ? RenderDirectoryDetail(entry)
            : RenderFileDetail(entry.Node);
    }

    private IRenderable RenderDirectoryDetail(TreeEntry entry)
    {
        var counts = InitializeStatusCounts();
        CountDescendants(entry.Node, counts);

        var table = new Table
        {
            Border = TableBorder.None
        };
        table.AddColumn(new TableColumn("Status"));
        table.AddColumn(new TableColumn("Count"));

        foreach (var pair in counts)
        {
            table.AddRow(new Markup($"{GetStatusGlyph(pair.Key)} {pair.Key}"), new Markup(pair.Value.ToString()));
        }

        var highlights = entry.Node.Children
            .Where(child => child.Status != ComparisonStatus.Equal)
            .Take(5)
            .Select(child => $"{GetStatusGlyph(child.Status)} {Markup.Escape(child.Name)}");

        var content = new Grid();
        content.AddColumn();
        content.AddRow(table);

        if (highlights.Any())
        {
            content.AddRow(new Markup("[bold]Highlighted Children[/]"));
            content.AddRow(new Markup(string.Join(Environment.NewLine, highlights)));
        }

        return new Panel(content)
            .Border(BoxBorder.Rounded)
            .Header($" Directory — {Markup.Escape(GetNodePath(entry.Node))} ");
    }

    private IRenderable RenderFileDetail(ComparisonNode node)
    {
        if (node.Detail is null)
        {
            return new Panel(new Markup("No file details available."))
                .Border(BoxBorder.Rounded)
                .Header($" File — {Markup.Escape(GetNodePath(node))} ");
        }

        var table = new Table
        {
            Border = TableBorder.None
        };
        table.AddColumn("Property");
        table.AddColumn("Left");
        table.AddColumn("Right");

        table.AddRow("Size", FormatSize(node.Detail.LeftSize), FormatSize(node.Detail.RightSize));
        table.AddRow("Modified", FormatDate(node.Detail.LeftModified), FormatDate(node.Detail.RightModified));

        var algorithms = GetAlgorithms(node.Detail);
        var active = GetActiveAlgorithmType();

        foreach (var algorithm in algorithms)
        {
            string? leftHash = null;
            if (node.Detail.LeftHashes is not null)
            {
                node.Detail.LeftHashes.TryGetValue(algorithm, out leftHash);
            }

            string? rightHash = null;
            if (node.Detail.RightHashes is not null)
            {
                node.Detail.RightHashes.TryGetValue(algorithm, out rightHash);
            }

            var label = algorithm.ToString();
            if (active == algorithm)
            {
                label = $"[{_theme.Accent}]{label}[/]";
            }

            table.AddRow($"{label} Hash", leftHash ?? "-", rightHash ?? "-");
        }

        if (!string.IsNullOrWhiteSpace(node.Detail.ErrorMessage))
        {
            table.AddRow("Error", node.Detail.ErrorMessage, node.Detail.ErrorMessage);
        }

        return new Panel(table)
            .Border(BoxBorder.Rounded)
            .Header($" File — {Markup.Escape(GetNodePath(node))} ");
    }

    private IRenderable RenderFooter()
    {
        var grid = new Grid();
        grid.AddColumn();

        grid.AddRow(new Markup($"[{_theme.Muted}]↑/↓ Move  ← Collapse  → Expand  Enter Toggle  F Filter  A Algorithm  R Re-run  E Export  D Diff  P Pause  L Verbosity  ? Help  Q Quit[/]"));

        if (!string.IsNullOrWhiteSpace(_statusMessage))
        {
            grid.AddRow(new Markup($"[{_theme.Accent}]{Markup.Escape(_statusMessage)}[/]"));
        }

        if (_showHelp)
        {
            grid.AddRow(new Markup("[bold]Shortcuts[/]"));
            grid.AddRow(new Markup(string.Join(Environment.NewLine, new[]
            {
                "F — Choose filter by status",
                "A — Switch active hash algorithm and re-run",
                "R — Re-run the comparison",
                "E — Export current view to json/csv/markdown",
                "D — Launch configured diff tool for the selected file",
                "P — Toggle pause banner",
                "L — Cycle verbosity levels",
                "Q — Quit interactive mode"
            })));
        }

        return new Panel(grid)
            .Border(BoxBorder.Rounded)
            .Header(" Controls ");
    }

    private void BuildTree(string? preservePath = null)
    {
        _rootEntry = BuildEntry(_result.Root, null, 0);
        RefreshVisibleEntries(preservePath ?? _rootEntry.Path);
    }

    private TreeEntry BuildEntry(ComparisonNode node, TreeEntry? parent, int depth)
    {
        var entry = new TreeEntry(node, parent, depth, GetNodePath(node))
        {
            Expanded = depth == 0
        };

        foreach (var child in node.Children)
        {
            entry.Children.Add(BuildEntry(child, entry, depth + 1));
        }

        return entry;
    }

    private void RefreshVisibleEntries(string? preservePath)
    {
        _visibleEntries.Clear();

        if (_rootEntry is null)
        {
            return;
        }

        var buffer = new List<TreeEntry>();
        AppendVisible(_rootEntry, buffer);
        _visibleEntries.AddRange(buffer);

        if (_visibleEntries.Count == 0)
        {
            _selectedIndex = 0;
            return;
        }

        if (!string.IsNullOrWhiteSpace(preservePath))
        {
            var index = _visibleEntries.FindIndex(entry => string.Equals(entry.Path, preservePath, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                _selectedIndex = index;
                return;
            }
        }

        if (_selectedIndex >= _visibleEntries.Count)
        {
            _selectedIndex = _visibleEntries.Count - 1;
        }

        if (_selectedIndex < 0)
        {
            _selectedIndex = 0;
        }
    }

    private bool AppendVisible(TreeEntry entry, List<TreeEntry> list)
    {
        var childEntries = new List<TreeEntry>();
        var hasVisibleChild = false;

        foreach (var child in entry.Children)
        {
            var childBuffer = new List<TreeEntry>();
            if (AppendVisible(child, childBuffer))
            {
                hasVisibleChild = true;
                childEntries.AddRange(childBuffer);
            }
        }

        var include = entry == _rootEntry || MatchesFilter(entry.Node) || hasVisibleChild;
        if (!include)
        {
            return false;
        }

        list.Add(entry);

        if (entry.Expanded)
        {
            list.AddRange(childEntries);
        }

        return true;
    }

    private void MoveSelection(int offset)
    {
        if (_visibleEntries.Count == 0)
        {
            return;
        }

        var index = Math.Clamp(_selectedIndex + offset, 0, _visibleEntries.Count - 1);
        _selectedIndex = index;
    }

    private void MoveToIndex(int index)
    {
        if (_visibleEntries.Count == 0)
        {
            return;
        }

        _selectedIndex = Math.Clamp(index, 0, _visibleEntries.Count - 1);
    }

    private void HandleCollapse()
    {
        var entry = GetSelectedEntry();
        if (entry.Node.NodeType == ComparisonNodeType.Directory && entry.Expanded)
        {
            entry.Expanded = false;
            RefreshVisibleEntries(entry.Path);
            return;
        }

        if (entry.Parent is not null)
        {
            var parentIndex = _visibleEntries.FindIndex(e => ReferenceEquals(e, entry.Parent));
            if (parentIndex >= 0)
            {
                _selectedIndex = parentIndex;
            }
        }
    }

    private void HandleExpand()
    {
        var entry = GetSelectedEntry();
        if (entry.Node.NodeType != ComparisonNodeType.Directory)
        {
            return;
        }

        if (!entry.Expanded)
        {
            entry.Expanded = true;
            RefreshVisibleEntries(entry.Path);
        }
        else
        {
            var child = _visibleEntries.FirstOrDefault(e => ReferenceEquals(e.Parent, entry));
            if (child is not null)
            {
                var index = _visibleEntries.IndexOf(child);
                if (index >= 0)
                {
                    _selectedIndex = index;
                }
            }
        }
    }

    private void ToggleExpansion()
    {
        var entry = GetSelectedEntry();
        if (entry.Node.NodeType != ComparisonNodeType.Directory)
        {
            return;
        }

        entry.Expanded = !entry.Expanded;
        RefreshVisibleEntries(entry.Path);
    }

    private async Task HandleFilterAsync()
    {
        var prompt = new SelectionPrompt<InteractiveFilter>()
            .Title("Select filter")
            .UseConverter(filter => filter.ToDisplayName());

        prompt.AddChoices(Enum.GetValues<InteractiveFilter>());

        var choice = AnsiConsole.Prompt(prompt);
        _filter = choice;
        RefreshVisibleEntries(GetSelectedEntry().Path);
        SetStatus($"Filter set to {choice.ToDisplayName()}.");
        await Task.CompletedTask;
    }

    private async Task HandleAlgorithmAsync()
    {
        if (_algorithmNames.Count <= 1)
        {
            SetStatus("Only one algorithm is configured.");
            return;
        }

        var prompt = new SelectionPrompt<string>()
            .Title("Select hash algorithm")
            .AddChoices(_algorithmNames);

        var choice = AnsiConsole.Prompt(prompt);
        var index = _algorithmNames.FindIndex(name => name.Equals(choice, StringComparison.OrdinalIgnoreCase));
        if (index < 0 || index == _activeAlgorithmIndex)
        {
            SetStatus($"Algorithm already set to {choice}.");
            return;
        }

        await ReRunComparisonAsync(input => input with
        {
            Algorithm = choice,
            AdditionalAlgorithms = ImmutableArray<string>.Empty
        });

        _activeAlgorithmIndex = index;
        SetStatus($"Re-ran comparison with {choice}.");
    }

    private async Task HandleRerunAsync()
    {
        await ReRunComparisonAsync(static input => input);
        SetStatus("Comparison refreshed.");
    }

    private async Task HandleExportAsync()
    {
        var nodes = CollectFilteredNodes();
        if (nodes.Count == 0)
        {
            SetStatus("Nothing to export for the current filter.");
            return;
        }

        var format = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title("Select export format")
            .AddChoices("json", "csv", "markdown"));

        var destination = AnsiConsole.Ask<string>("Enter output path:");

        try
        {
            await _exportService.ExportAsync(
                _result,
                nodes,
                format,
                destination,
                GetActiveAlgorithmType(),
                _cancellationToken);

            SetStatus($"Exported {nodes.Count} entries to {destination}.");
        }
        catch (OperationCanceledException)
        {
            SetStatus("Export cancelled.");
        }
        catch (Exception ex)
        {
            SetStatus($"Export failed: {ex.Message}");
        }
    }

    private void HandleDiff()
    {
        var entry = GetSelectedEntry();
        if (entry.Node.NodeType != ComparisonNodeType.File)
        {
            SetStatus("Select a file to launch the diff tool.");
            return;
        }

        if (entry.Node.Status is ComparisonStatus.LeftOnly or ComparisonStatus.RightOnly)
        {
            SetStatus("Diff is only available when both files exist.");
            return;
        }

        var diffTool = _resolved.DiffTool;
        var leftPath = Path.Combine(_result.LeftPath, entry.Node.RelativePath);
        var rightPath = Path.Combine(_result.RightPath, entry.Node.RelativePath);

        var (success, message) = _diffLauncher.TryLaunch(diffTool, leftPath, rightPath);
        SetStatus(message);

        if (!success && diffTool is null)
        {
            SetStatus("Configure a diff tool via --diff-tool or configuration to enable this shortcut.");
        }
    }

    private void TogglePause()
    {
        _paused = !_paused;
        SetStatus(_paused ? "Session paused." : "Session resumed.");
    }

    private void CycleVerbosity()
    {
        _verbosity = _verbosity.Next();
        SetStatus($"Verbosity set to {_verbosity.ToDisplayName()}.");
    }

    private async Task ReRunComparisonAsync(Func<CompareSettingsInput, CompareSettingsInput> mutate)
    {
        try
        {
            var previousPath = GetSelectedEntry().Path;
            var updatedInput = mutate(_input) with
            {
                EnableInteractive = true,
                InteractiveFilter = _input.InteractiveFilter,
                InteractiveTheme = _input.InteractiveTheme,
                InteractiveVerbosity = _input.InteractiveVerbosity
            };

            await AnsiConsole.Status()
                .StartAsync("Running comparison...", async _ =>
                {
                    var run = await _orchestrator.RunAsync(updatedInput, _cancellationToken);
                    _result = run.Result;
                    _resolved = run.Resolved;
                    _input = updatedInput;
                    _lastRunAt = DateTimeOffset.UtcNow;
                    UpdateAlgorithmList(_input.Algorithm);
                    BuildTree(previousPath);
                });
        }
        catch (OperationCanceledException)
        {
            SetStatus("Comparison cancelled.");
        }
        catch (Exception ex)
        {
            SetStatus($"Comparison failed: {ex.Message}");
        }
    }

    private void UpdateAlgorithmList(string? preferred)
    {
        _algorithmTypes.Clear();
        _algorithmNames.Clear();

        if (!_resolved.Algorithms.IsDefaultOrEmpty)
        {
            foreach (var algorithm in _resolved.Algorithms)
            {
                _algorithmTypes.Add(algorithm);
                _algorithmNames.Add(NormalizeAlgorithmName(algorithm));
            }
        }

        if (_algorithmNames.Count == 0)
        {
            var fallback = _resolved.Mode == ComparisonMode.Hash ? HashAlgorithmType.Sha256 : HashAlgorithmType.Crc32;
            _algorithmTypes.Add(fallback);
            _algorithmNames.Add(NormalizeAlgorithmName(fallback));
        }

        if (!string.IsNullOrWhiteSpace(preferred))
        {
            var index = _algorithmNames.FindIndex(name => name.Equals(preferred, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                _activeAlgorithmIndex = index;
                return;
            }
        }

        if (_activeAlgorithmIndex >= _algorithmNames.Count || _activeAlgorithmIndex < 0)
        {
            _activeAlgorithmIndex = 0;
        }
    }

    private TreeEntry GetSelectedEntry()
    {
        if (_visibleEntries.Count == 0)
        {
            return _rootEntry ?? throw new InvalidOperationException("Tree has not been initialized.");
        }

        _selectedIndex = Math.Clamp(_selectedIndex, 0, _visibleEntries.Count - 1);
        return _visibleEntries[_selectedIndex];
    }

    private List<ComparisonNode> CollectFilteredNodes()
    {
        var nodes = new List<ComparisonNode>();

        void Visit(ComparisonNode node)
        {
            if (MatchesFilter(node))
            {
                nodes.Add(node);
            }

            foreach (var child in node.Children)
            {
                Visit(child);
            }
        }

        Visit(_result.Root);
        return nodes;
    }

    private bool MatchesFilter(ComparisonNode node)
        => _filter switch
        {
            InteractiveFilter.All => true,
            InteractiveFilter.Differences => node.Status is ComparisonStatus.Different or ComparisonStatus.LeftOnly or ComparisonStatus.RightOnly or ComparisonStatus.Error,
            InteractiveFilter.LeftOnly => node.Status == ComparisonStatus.LeftOnly,
            InteractiveFilter.RightOnly => node.Status == ComparisonStatus.RightOnly,
            InteractiveFilter.Errors => node.Status == ComparisonStatus.Error,
            _ => true
        };

    private static Dictionary<ComparisonStatus, int> InitializeStatusCounts()
        => new()
        {
            [ComparisonStatus.Equal] = 0,
            [ComparisonStatus.Different] = 0,
            [ComparisonStatus.LeftOnly] = 0,
            [ComparisonStatus.RightOnly] = 0,
            [ComparisonStatus.Error] = 0
        };

    private static void CountDescendants(ComparisonNode node, IDictionary<ComparisonStatus, int> counts)
    {
        if (node.NodeType == ComparisonNodeType.File)
        {
            counts[node.Status] = counts[node.Status] + 1;
            return;
        }

        foreach (var child in node.Children)
        {
            CountDescendants(child, counts);
        }
    }

    private IEnumerable<HashAlgorithmType> GetAlgorithms(FileComparisonDetail detail)
    {
        var set = new HashSet<HashAlgorithmType>();
        if (detail.LeftHashes is not null)
        {
            foreach (var key in detail.LeftHashes.Keys)
            {
                set.Add(key);
            }
        }

        if (detail.RightHashes is not null)
        {
            foreach (var key in detail.RightHashes.Keys)
            {
                set.Add(key);
            }
        }

        return set.OrderBy(algorithm => algorithm.ToString(), StringComparer.OrdinalIgnoreCase);
    }

    private string BuildTreeRow(TreeEntry entry)
    {
        var indent = new string(' ', entry.Depth * 2);
        var expander = entry.Node.NodeType == ComparisonNodeType.Directory
            ? $"[{_theme.Muted}]{(entry.Expanded ? '▾' : '▸')}[/]"
            : " ";
        var status = GetStatusGlyph(entry.Node.Status);
        var nameColor = GetStatusColor(entry.Node.Status);
        var nameMarkup = $"[{nameColor}]{Markup.Escape(entry.Node.Name)}[/]";

        var row = $"{indent}{expander} {status} {nameMarkup}";
        if (ReferenceEquals(entry, GetSelectedEntry()))
        {
            row = $"[reverse]{row}[/]";
        }

        return row;
    }

    private string GetStatusGlyph(ComparisonStatus status)
        => status switch
        {
            ComparisonStatus.Equal => $"[{_theme.Equal}]✓[/]",
            ComparisonStatus.Different => $"[{_theme.Different}]![/]",
            ComparisonStatus.LeftOnly => $"[{_theme.LeftOnly}]←[/]",
            ComparisonStatus.RightOnly => $"[{_theme.RightOnly}]→[/]",
            ComparisonStatus.Error => $"[{_theme.Error}]?[/]",
            _ => status.ToString()
        };

    private string GetStatusColor(ComparisonStatus status)
        => status switch
        {
            ComparisonStatus.Equal => _theme.Equal,
            ComparisonStatus.Different => _theme.Different,
            ComparisonStatus.LeftOnly => _theme.LeftOnly,
            ComparisonStatus.RightOnly => _theme.RightOnly,
            ComparisonStatus.Error => _theme.Error,
            _ => _theme.Muted
        };

    private static string GetNodePath(ComparisonNode node)
        => string.IsNullOrWhiteSpace(node.RelativePath)
            ? node.Name
            : node.RelativePath.Replace(Path.DirectorySeparatorChar, '/');

    private static string FormatSize(long? value)
        => value is null ? "-" : value.Value.ToString("N0");

    private static string FormatDate(DateTimeOffset? value)
        => value is null ? "-" : value.Value.ToString("u");

    private string GetActiveAlgorithmName()
    {
        if (_algorithmNames.Count == 0)
        {
            return "n/a";
        }

        _activeAlgorithmIndex = Math.Clamp(_activeAlgorithmIndex, 0, _algorithmNames.Count - 1);
        return _algorithmNames[_activeAlgorithmIndex];
    }

    private HashAlgorithmType? GetActiveAlgorithmType()
    {
        if (_algorithmTypes.Count == 0)
        {
            return null;
        }

        _activeAlgorithmIndex = Math.Clamp(_activeAlgorithmIndex, 0, _algorithmTypes.Count - 1);
        return _algorithmTypes[_activeAlgorithmIndex];
    }

    private static string NormalizeAlgorithmName(HashAlgorithmType algorithm)
        => algorithm switch
        {
            HashAlgorithmType.Crc32 => "crc32",
            HashAlgorithmType.Md5 => "md5",
            HashAlgorithmType.Sha256 => "sha256",
            HashAlgorithmType.XxHash64 => "xxhash64",
            _ => algorithm.ToString().ToLowerInvariant()
        };

    private string FormatThreads()
        => _resolved.Threads?.ToString() ?? "auto";

    private string FormatDiffTool()
        => string.IsNullOrWhiteSpace(_resolved.DiffTool)
            ? "not configured"
            : _resolved.DiffTool!;

    private void SetStatus(string message)
    {
        _statusMessage = message;
    }

    private sealed class TreeEntry
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TreeEntry"/> class.
        /// </summary>
        /// <param name="node">Comparison node represented by the entry.</param>
        /// <param name="parent">Parent entry in the tree.</param>
        /// <param name="depth">Depth of the node in the tree.</param>
        /// <param name="path">Relative path of the node.</param>
        public TreeEntry(ComparisonNode node, TreeEntry? parent, int depth, string path)
        {
            Node = node;
            Parent = parent;
            Depth = depth;
            Path = path;
        }

        /// <summary>
        /// Gets the comparison node associated with the entry.
        /// </summary>
        public ComparisonNode Node { get; }

        /// <summary>
        /// Gets the parent entry in the tree, if any.
        /// </summary>
        public TreeEntry? Parent { get; }

        /// <summary>
        /// Gets the depth of the entry in the tree.
        /// </summary>
        public int Depth { get; }

        /// <summary>
        /// Gets the relative path represented by the entry.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the entry is expanded in the UI.
        /// </summary>
        public bool Expanded { get; set; }

        /// <summary>
        /// Gets the child entries beneath this node.
        /// </summary>
        public List<TreeEntry> Children { get; } = new();
    }
}
