using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ParallelCompare.App.Interactive;
using ParallelCompare.App.Rendering;
using ParallelCompare.App.Services;
using ParallelCompare.Core.Options;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ParallelCompare.App.Commands;

public sealed class WatchCommand : AsyncCommand<WatchCommandSettings>
{
    private readonly ComparisonOrchestrator _orchestrator;
    private readonly DiffToolLauncher _diffLauncher;

    public WatchCommand(ComparisonOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
        _diffLauncher = new DiffToolLauncher();
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

            if (settings.Interactive)
            {
                await RunInteractiveAsync(resolvedRun.Result, input, resolvedRun.Resolved, settings, cancellation.Token);
                return 0;
            }

            Render(resolvedRun.Result, resolvedRun.Resolved, settings);

            using var semaphore = new SemaphoreSlim(1, 1);
            using var timer = new Timer(async _ => await RunComparisonAsync(), null, Timeout.Infinite, Timeout.Infinite);

            using var leftWatcher = CreateWatcher(resolvedRun.Resolved.LeftPath, ScheduleRun);
            using FileSystemWatcher? rightWatcher = !resolvedRun.Resolved.UsesBaseline && !string.IsNullOrWhiteSpace(resolvedRun.Resolved.RightPath)
                ? CreateWatcher(resolvedRun.Resolved.RightPath!, ScheduleRun)
                : null;

            leftWatcher.EnableRaisingEvents = true;
            if (rightWatcher is not null)
            {
                rightWatcher.EnableRaisingEvents = true;
            }

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
                    Render(run.Result, run.Resolved, settings);
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

    private async Task RunInteractiveAsync(
        Core.Comparison.ComparisonResult result,
        CompareSettingsInput input,
        ResolvedCompareSettings resolved,
        WatchCommandSettings settings,
        CancellationToken cancellationToken)
    {
        var interactiveInput = input with
        {
            InteractiveFilter = resolved.InteractiveFilter,
            InteractiveTheme = resolved.InteractiveTheme,
            InteractiveVerbosity = resolved.InteractiveVerbosity
        };

        var session = new InteractiveCompareSession(_orchestrator, _diffLauncher);

        using var timer = new Timer(async _ => await TriggerRefreshAsync(), null, Timeout.Infinite, Timeout.Infinite);

        using var leftWatcher = CreateWatcher(resolved.LeftPath, ScheduleRun);
        using FileSystemWatcher? rightWatcher = !resolved.UsesBaseline && !string.IsNullOrWhiteSpace(resolved.RightPath)
            ? CreateWatcher(resolved.RightPath!, ScheduleRun)
            : null;

        leftWatcher.EnableRaisingEvents = true;
        if (rightWatcher is not null)
        {
            rightWatcher.EnableRaisingEvents = true;
        }

        AnsiConsole.MarkupLine("[grey]Watching for changes. Press Ctrl+C to stop.[/]");

        var initialStatus = resolved.UsesBaseline && resolved.BaselineMetadata is { } baseline
            ? $"Watching {resolved.LeftPath} against baseline {baseline.ManifestPath}."
            : $"Watching {resolved.LeftPath} and {resolved.RightPath}.";

        await session.RunAsync(result, interactiveInput, resolved, cancellationToken, initialStatus);

        return;

        void ScheduleRun()
        {
            timer.Change(TimeSpan.FromMilliseconds(settings.DebounceMilliseconds), Timeout.InfiniteTimeSpan);
        }

        async Task TriggerRefreshAsync()
        {
            var timestamp = DateTimeOffset.Now.ToLocalTime().ToString("T");
            var refreshed = $"Filesystem change detected at {timestamp}. Comparison refreshed.";
            var paused = $"Filesystem change detected at {timestamp}, but refresh is paused.";
            await session.QueueWatchRefreshAsync(refreshed, paused);
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

    private static void Render(Core.Comparison.ComparisonResult result, ResolvedCompareSettings resolved, WatchCommandSettings settings)
    {
        AnsiConsole.Clear();
        ComparisonConsoleRenderer.RenderSummary(result);
        ComparisonConsoleRenderer.RenderWatchStatus(result, resolved);
        if (!settings.Interactive)
        {
            ComparisonConsoleRenderer.RenderTree(result, maxDepth: 2);
        }
    }
}
