using System;
using System.Threading;
using ParallelCompare.App.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ParallelCompare.App.Commands;

public sealed class SnapshotCommand : AsyncCommand<SnapshotCommandSettings>
{
    private readonly ComparisonOrchestrator _orchestrator;

    public SnapshotCommand(ComparisonOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, SnapshotCommandSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Output))
        {
            AnsiConsole.MarkupLine("[red]--output is required.[/]");
            return 2;
        }

        var input = _orchestrator.BuildInput(settings) with
        {
            JsonReportPath = settings.Output
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

            await _orchestrator.RunAsync(input, cancellation.Token);
            AnsiConsole.MarkupLine($"[green]Snapshot written to {settings.Output}.[/]");
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
