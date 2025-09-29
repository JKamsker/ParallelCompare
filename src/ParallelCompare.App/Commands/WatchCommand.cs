using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ParallelCompare.App.Rendering;
using ParallelCompare.App.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ParallelCompare.App.Commands;

public sealed class WatchCommand : AsyncCommand<WatchCommandSettings>
{
    private readonly ComparisonOrchestrator _orchestrator;

    public WatchCommand(ComparisonOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, WatchCommandSettings settings)
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

            var resolvedRun = await _orchestrator.RunAsync(input, cancellation.Token);
            Render(resolvedRun.Result, settings);

            using var semaphore = new SemaphoreSlim(1, 1);
            using var timer = new Timer(async _ => await RunComparisonAsync(), null, Timeout.Infinite, Timeout.Infinite);

            using var leftWatcher = CreateWatcher(resolvedRun.Resolved.LeftPath, ScheduleRun);
            using var rightWatcher = CreateWatcher(resolvedRun.Resolved.RightPath, ScheduleRun);

            leftWatcher.EnableRaisingEvents = true;
            rightWatcher.EnableRaisingEvents = true;

            AnsiConsole.MarkupLine("[grey]Watching for changes. Press Ctrl+C to stop.[/]");
            try
            {
                await Task.Delay(Timeout.Infinite, cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected on cancellation
            }

            return 0;

            void ScheduleRun()
            {
                timer.Change(TimeSpan.FromMilliseconds(settings.DebounceMilliseconds), Timeout.InfiniteTimeSpan);
            }

            async Task RunComparisonAsync()
            {
                if (!await semaphore.WaitAsync(0, cancellation.Token))
                {
                    return;
                }

                try
                {
                    var run = await _orchestrator.RunAsync(input, cancellation.Token);
                    Render(run.Result, settings);
                }
                catch (OperationCanceledException)
                {
                    // Ignore cancellation during shutdown.
                }
                finally
                {
                    semaphore.Release();
                }
            }
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[yellow]Watch cancelled.[/]");
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

    private static FileSystemWatcher CreateWatcher(string path, Action onChange)
    {
        var watcher = new FileSystemWatcher(path)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
        };

        void Handler(object? sender, FileSystemEventArgs args) => onChange();
        void RenamedHandler(object? sender, RenamedEventArgs args) => onChange();

        watcher.Changed += Handler;
        watcher.Created += Handler;
        watcher.Deleted += Handler;
        watcher.Renamed += RenamedHandler;

        return watcher;
    }

    private static void Render(Core.Comparison.ComparisonResult result, WatchCommandSettings settings)
    {
        AnsiConsole.Clear();
        ComparisonConsoleRenderer.RenderSummary(result);
        if (!settings.Interactive)
        {
            ComparisonConsoleRenderer.RenderTree(result, maxDepth: 2);
        }
    }
}
