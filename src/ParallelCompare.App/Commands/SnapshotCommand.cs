using System;
using System.Threading;
using ParallelCompare.App.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ParallelCompare.App.Commands;

/// <summary>
/// Implements the <c>snapshot</c> command that captures baseline manifests.
/// </summary>
public sealed class SnapshotCommand : AsyncCommand<SnapshotCommandSettings>
{
    private readonly ComparisonOrchestrator _orchestrator;

    /// <summary>
    /// Initializes a new instance of the <see cref="SnapshotCommand"/> class.
    /// </summary>
    /// <param name="orchestrator">Service responsible for executing comparisons.</param>
    public SnapshotCommand(ComparisonOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, SnapshotCommandSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Output))
        {
            AnsiConsole.MarkupLine("[red]--output is required.[/]");
            return 2;
        }

        var baseInput = _orchestrator.BuildInput(settings);
        var input = baseInput with
        {
            RightPath = baseInput.LeftPath
        };

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

            var manifest = await _orchestrator.CreateSnapshotAsync(input, settings.Output, cancellation.Token);
            AnsiConsole.MarkupLine($"[green]Snapshot written to {settings.Output} (captured {manifest.CreatedAt:u}).[/]");
            return 0;
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[yellow]Snapshot cancelled.[/]");
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
}
