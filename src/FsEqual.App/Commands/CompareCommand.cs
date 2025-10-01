using System;
using System.Threading;
using FsEqual.App.Interactive;
using FsEqual.App.Rendering;
using FsEqual.App.Services;
using FsEqual.Core.Options;
using Spectre.Console;
using Spectre.Console.Cli;

namespace FsEqual.App.Commands;

/// <summary>
/// Implements the <c>compare</c> command that compares two directories or a baseline.
/// </summary>
public sealed class CompareCommand : AsyncCommand<CompareCommandSettings>
{
    private readonly ComparisonOrchestrator _orchestrator;
    private readonly DiffToolLauncher _diffLauncher;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompareCommand"/> class.
    /// </summary>
    /// <param name="orchestrator">Service responsible for executing comparisons.</param>
    public CompareCommand(ComparisonOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
        _diffLauncher = new DiffToolLauncher();
    }

    /// <inheritdoc />
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
                : await ComparisonProgressConsole.RunAsync(_orchestrator, input, cancellation.Token);

            ComparisonConsoleRenderer.RenderSummary(result);

            if (settings.Interactive)
            {
                await LaunchInteractiveAsync(result, input, resolved, cancellation.Token);
            }
            else
            {
                var summaryFilter = InteractiveFilterExtensions.Parse(resolved.SummaryFilter);
                ComparisonConsoleRenderer.RenderTree(result, summaryFilter);
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

    private async Task LaunchInteractiveAsync(
        Core.Comparison.ComparisonResult result,
        CompareSettingsInput input,
        ResolvedCompareSettings resolved,
        CancellationToken cancellationToken)
    {
        var interactiveInput = input with
        {
            InteractiveFilter = resolved.InteractiveFilter,
            InteractiveTheme = resolved.InteractiveTheme,
            InteractiveVerbosity = resolved.InteractiveVerbosity
        };

        var session = new InteractiveCompareSession(_orchestrator, _diffLauncher);
        await session.RunAsync(result, interactiveInput, resolved, cancellationToken);
    }
}
