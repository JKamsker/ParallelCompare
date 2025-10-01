using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FsEqual.App.Interactive;
using FsEqual.App.Rendering;
using FsEqual.App.Services;
using FsEqual.Core.Options;
using Microsoft.Extensions.FileSystemGlobbing;
using Spectre.Console;
using Spectre.Console.Cli;

namespace FsEqual.App.Commands;

/// <summary>
/// Implements the <c>watch</c> command that monitors directories and reruns comparisons.
/// </summary>
public sealed class WatchCommand : AsyncCommand<WatchCommandSettings>
{
    private readonly ComparisonOrchestrator _orchestrator;
    private readonly DiffToolLauncher _diffLauncher;

    /// <summary>
    /// Initializes a new instance of the <see cref="WatchCommand"/> class.
    /// </summary>
    /// <param name="orchestrator">Service responsible for executing comparisons.</param>
    public WatchCommand(ComparisonOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
        _diffLauncher = new DiffToolLauncher();
    }

    /// <inheritdoc />
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
            var lastSuccessfulRun = DateTimeOffset.Now;

            if (settings.Interactive)
            {
                await RunInteractiveAsync(resolvedRun.Result, input, resolvedRun.Resolved, settings, cancellation.Token);
                return 0;
            }

            Render(resolvedRun.Result, resolvedRun.Resolved, settings, lastSuccessfulRun);

            using var semaphore = new SemaphoreSlim(1, 1);
            using var timer = new Timer(async _ => await RunComparisonAsync(), null, Timeout.Infinite, Timeout.Infinite);
            TimeSpan debounceInterval = ResolveDebounceInterval(settings, resolvedRun.Resolved);
            Matcher? leftMatcher = CreateIgnoreMatcher(resolvedRun.Resolved);
            Matcher? rightMatcher = CreateIgnoreMatcher(resolvedRun.Resolved);

            using var leftWatcher = CreateWatcher(resolvedRun.Resolved.LeftPath, () => leftMatcher, ScheduleRun);
            using FileSystemWatcher? rightWatcher = !resolvedRun.Resolved.UsesBaseline && !string.IsNullOrWhiteSpace(resolvedRun.Resolved.RightPath)
                ? CreateWatcher(resolvedRun.Resolved.RightPath!, () => rightMatcher, ScheduleRun)
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
                timer.Change(debounceInterval, Timeout.InfiniteTimeSpan);
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
                    debounceInterval = ResolveDebounceInterval(settings, run.Resolved);
                    leftMatcher = CreateIgnoreMatcher(run.Resolved);
                    rightMatcher = CreateIgnoreMatcher(run.Resolved);
                    lastSuccessfulRun = DateTimeOffset.Now;
                    Render(run.Result, run.Resolved, settings, lastSuccessfulRun);
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
        TimeSpan debounceInterval = ResolveDebounceInterval(settings, resolved);
        Matcher? leftMatcher = CreateIgnoreMatcher(resolved);
        Matcher? rightMatcher = CreateIgnoreMatcher(resolved);

        using var leftWatcher = CreateWatcher(resolved.LeftPath, () => leftMatcher, ScheduleRun);
        using FileSystemWatcher? rightWatcher = !resolved.UsesBaseline && !string.IsNullOrWhiteSpace(resolved.RightPath)
            ? CreateWatcher(resolved.RightPath!, () => rightMatcher, ScheduleRun)
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
            timer.Change(debounceInterval, Timeout.InfiniteTimeSpan);
        }

        async Task TriggerRefreshAsync()
        {
            var timestamp = DateTimeOffset.Now.ToLocalTime().ToString("T");
            var refreshed = $"Filesystem change detected at {timestamp}. Comparison refreshed.";
            var paused = $"Filesystem change detected at {timestamp}, but refresh is paused.";
            await session.QueueWatchRefreshAsync(refreshed, paused);
        }
    }

    private static FileSystemWatcher CreateWatcher(string path, Func<Matcher?> matcherProvider, Action onChange)
    {
        var watcher = new FileSystemWatcher(path)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
        };

        void Handler(object? sender, FileSystemEventArgs args)
        {
            if (ShouldTriggerChange(path, args.FullPath, null, matcherProvider()))
            {
                onChange();
            }
        }

        void RenamedHandler(object? sender, RenamedEventArgs args)
        {
            if (ShouldTriggerChange(path, args.FullPath, args.OldFullPath, matcherProvider()))
            {
                onChange();
            }
        }

        watcher.Changed += Handler;
        watcher.Created += Handler;
        watcher.Deleted += Handler;
        watcher.Renamed += RenamedHandler;

        return watcher;
    }

    private static void Render(
        Core.Comparison.ComparisonResult result,
        ResolvedCompareSettings resolved,
        WatchCommandSettings settings,
        DateTimeOffset lastSuccessfulRun)
    {
        AnsiConsole.Clear();
        ComparisonConsoleRenderer.RenderSummary(result);
        ComparisonConsoleRenderer.RenderWatchStatus(result, resolved, lastSuccessfulRun);
        if (!settings.Interactive)
        {
            ComparisonConsoleRenderer.RenderTree(result, maxDepth: 2);
        }
    }

    internal static TimeSpan ResolveDebounceInterval(WatchCommandSettings settings, ResolvedCompareSettings resolved)
    {
        var milliseconds = settings.DebounceMilliseconds
            ?? resolved.WatchDebounceMilliseconds
            ?? WatchCommandSettings.DefaultDebounceMilliseconds;

        if (milliseconds < 0)
        {
            milliseconds = 0;
        }

        return TimeSpan.FromMilliseconds(milliseconds);
    }

    internal static Matcher? CreateIgnoreMatcher(ResolvedCompareSettings resolved)
    {
        if (resolved.IgnorePatterns.IsDefaultOrEmpty)
        {
            return null;
        }

        var matcher = new Matcher(resolved.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
        matcher.AddInclude("**/*");
        foreach (var pattern in resolved.IgnorePatterns)
        {
            matcher.AddExclude(pattern);
        }

        return matcher;
    }

    internal static bool ShouldTriggerChange(string root, string? newPath, string? oldPath, Matcher? matcher)
    {
        if (matcher is null)
        {
            return true;
        }

        return IsRelevantPath(root, newPath, matcher) || IsRelevantPath(root, oldPath, matcher);
    }

    private static bool IsRelevantPath(string root, string? candidatePath, Matcher matcher)
    {
        if (string.IsNullOrWhiteSpace(candidatePath))
        {
            return false;
        }

        string relative;
        try
        {
            relative = Path.GetRelativePath(root, candidatePath);
        }
        catch (Exception)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(relative) || relative == "." || relative.StartsWith("..", StringComparison.Ordinal))
        {
            return false;
        }

        relative = relative.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

        return matcher.Match(relative).HasMatches;
    }
}
