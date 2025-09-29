using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ParallelCompare.App.Rendering;
using ParallelCompare.App.Services;
using ParallelCompare.Core.Options;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ParallelCompare.App.Commands;

public sealed class CompareCommand : AsyncCommand<CompareCommandSettings>
{
    private readonly ComparisonOrchestrator _orchestrator;

    public CompareCommand(ComparisonOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, CompareCommandSettings settings)
    {
        var input = _orchestrator.BuildInput(settings);
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler? handler = null;

        try
        {
            if (input.Timeout is { } timeout)
            {
                cancellation.CancelAfter(timeout);
            }

            handler = (_, e) =>
            {
                e.Cancel = true;
                cancellation.Cancel();
            };
            Console.CancelKeyPress += handler;

            (var result, var resolved) = settings.NoProgress
                ? await _orchestrator.RunAsync(input, cancellation.Token)
                : await AnsiConsole.Status()
                    .StartAsync("Comparing directories...", async _ => await _orchestrator.RunAsync(input, cancellation.Token));

            ComparisonConsoleRenderer.RenderSummary(result);

            if (settings.Interactive)
            {
                LaunchInteractive(result);
            }
            else
            {
                ComparisonConsoleRenderer.RenderTree(result);
            }

            return DetermineExitCode(resolved.FailOn, result);
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[yellow]Comparison cancelled.[/]");
            return 2;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything | ExceptionFormats.ShowLinks);
            return 2;
        }
        finally
        {
            if (handler is not null)
            {
                Console.CancelKeyPress -= handler;
            }
        }
    }

    private static int DetermineExitCode(string? failOn, Core.Comparison.ComparisonResult result)
    {
        var summary = result.Summary;
        var hasDifferences = summary.DifferentFiles > 0 || summary.LeftOnlyFiles > 0 || summary.RightOnlyFiles > 0;
        var hasErrors = summary.ErrorFiles > 0;

        if (hasErrors)
        {
            return 2;
        }

        var policy = string.IsNullOrWhiteSpace(failOn) ? "diff" : failOn.ToLowerInvariant();
        return policy switch
        {
            "error" => 0,
            "any" => hasDifferences ? 1 : 0,
            _ => hasDifferences ? 1 : 0
        };
    }

    private static void LaunchInteractive(Core.Comparison.ComparisonResult result)
    {
        var stack = new Stack<Core.Comparison.ComparisonNode>();
        var current = result.Root;

        while (true)
        {
            var prompt = new SelectionPrompt<InteractiveOption>()
                .Title($"[bold]{(string.IsNullOrEmpty(current.RelativePath) ? current.Name : current.RelativePath)}[/] — select a node")
                .PageSize(15)
                .UseConverter(option => option.Label);

            var options = BuildOptions(current, stack.Count > 0);
            prompt.AddChoices(options);

            var choice = AnsiConsole.Prompt(prompt);
            if (choice.IsExit)
            {
                return;
            }

            if (choice.IsBack)
            {
                current = stack.Pop();
                continue;
            }

            if (choice.Node is null)
            {
                continue;
            }

            if (choice.Node.NodeType == Core.Comparison.ComparisonNodeType.Directory)
            {
                stack.Push(current);
                current = choice.Node;
            }
            else
            {
                DisplayFileDetail(choice.Node);
            }
        }
    }

    private static IEnumerable<InteractiveOption> BuildOptions(Core.Comparison.ComparisonNode node, bool canGoBack)
    {
        if (canGoBack)
        {
            yield return InteractiveOption.Back;
        }

        foreach (var child in node.Children)
        {
            yield return new InteractiveOption(FormatLabel(child), child, false, false);
        }

        yield return InteractiveOption.Exit;
    }

    private static string FormatLabel(Core.Comparison.ComparisonNode node)
    {
        var status = node.Status switch
        {
            Core.Comparison.ComparisonStatus.Equal => "[green]Equal[/]",
            Core.Comparison.ComparisonStatus.Different => "[yellow]Different[/]",
            Core.Comparison.ComparisonStatus.LeftOnly => "[blue]Left Only[/]",
            Core.Comparison.ComparisonStatus.RightOnly => "[magenta]Right Only[/]",
            Core.Comparison.ComparisonStatus.Error => "[red]Error[/]",
            _ => node.Status.ToString()
        };

        return node.NodeType == Core.Comparison.ComparisonNodeType.Directory
            ? $"{node.Name} ({status})"
            : $"{node.Name} - {status}";
    }

    private static void DisplayFileDetail(Core.Comparison.ComparisonNode node)
    {
        if (node.Detail is null)
        {
            return;
        }

        var table = new Table().Title($"[bold]{node.Name}[/]");
        table.AddColumn("Property");
        table.AddColumn("Left");
        table.AddColumn("Right");

        table.AddRow("Size", node.Detail.LeftSize?.ToString() ?? "-", node.Detail.RightSize?.ToString() ?? "-");
        table.AddRow("Modified", node.Detail.LeftModified?.ToString("u") ?? "-", node.Detail.RightModified?.ToString("u") ?? "-");

        if (node.Detail.LeftHashes is not null || node.Detail.RightHashes is not null)
        {
            var allAlgorithms = new HashSet<HashAlgorithmType>();
            if (node.Detail.LeftHashes is not null)
            {
                allAlgorithms.UnionWith(node.Detail.LeftHashes.Keys);
            }

            if (node.Detail.RightHashes is not null)
            {
                allAlgorithms.UnionWith(node.Detail.RightHashes.Keys);
            }

            foreach (var algorithm in allAlgorithms.OrderBy(x => x.ToString()))
            {
                string? leftHash = null;
                if (node.Detail.LeftHashes is not null && node.Detail.LeftHashes.TryGetValue(algorithm, out var l))
                {
                    leftHash = l;
                }

                string? rightHash = null;
                if (node.Detail.RightHashes is not null && node.Detail.RightHashes.TryGetValue(algorithm, out var r))
                {
                    rightHash = r;
                }

                table.AddRow($"{algorithm} Hash", leftHash ?? "-", rightHash ?? "-");
            }
        }

        if (!string.IsNullOrWhiteSpace(node.Detail.ErrorMessage))
        {
            table.AddRow("Error", node.Detail.ErrorMessage, node.Detail.ErrorMessage);
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine("[grey]Press enter to return.[/]");
        Console.ReadLine();
    }

    private sealed record InteractiveOption(string Label, Core.Comparison.ComparisonNode? Node, bool IsExit, bool IsBack)
    {
        public static InteractiveOption Back { get; } = new("⬆ Back", null, false, true);
        public static InteractiveOption Exit { get; } = new("Exit", null, true, false);
    }
}
