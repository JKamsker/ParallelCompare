using System.Diagnostics;
using System.IO;
using FsEqual.Tool.Comparison;
using FsEqual.Tool.Reporting;
using Spectre.Console;
using Spectre.Console.Cli;

namespace FsEqual.Tool.Commands;

public sealed class WatchCommand : AsyncCommand<WatchSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, WatchSettings settings)
    {
        var options = settings.ToOptions();
        using var cts = new CancellationTokenSource();

        void OnCancel(object? sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            cts.Cancel();
        }

        Console.CancelKeyPress += OnCancel;

        try
        {
            var comparer = new DirectoryComparer();
            var reporterSettings = new CompareSettings
            {
                Left = options.Left,
                Right = options.Right,
                BaselinePath = options.BaselinePath
            };

            var debounce = TimeSpan.FromMilliseconds(Math.Max(100, settings.DebounceMilliseconds));
            long lastEventTicks = 0;
            var watchers = CreateWatchers(options, () => Interlocked.Exchange(ref lastEventTicks, Stopwatch.GetTimestamp()));
            try
            {
                await RunComparisonAsync(comparer, options, reporterSettings, cts.Token).ConfigureAwait(false);
                AnsiConsole.MarkupLine("[grey]Watching for changes. Press Ctrl+C to stop.[/]");

                while (!cts.Token.IsCancellationRequested)
                {
                    var snapshot = Interlocked.Read(ref lastEventTicks);
                    if (snapshot != 0 && Stopwatch.GetElapsedTime(snapshot) >= debounce)
                    {
                        if (Interlocked.CompareExchange(ref lastEventTicks, 0, snapshot) == snapshot)
                        {
                            await RunComparisonAsync(comparer, options, reporterSettings, cts.Token).ConfigureAwait(false);
                            AnsiConsole.MarkupLine("[grey]Watching for changes. Press Ctrl+C to stop.[/]");
                        }
                    }

                    await Task.Delay(250, cts.Token).ConfigureAwait(false);
                }
            }
            finally
            {
                foreach (var watcher in watchers)
                {
                    watcher.Dispose();
                }
            }
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[yellow]Watch cancelled.[/]");
        }
        finally
        {
            Console.CancelKeyPress -= OnCancel;
        }

        return 0;
    }

    private static async Task RunComparisonAsync(DirectoryComparer comparer, ComparisonOptions options, CompareSettings settings, CancellationToken cancellationToken)
    {
        var result = await comparer.CompareAsync(options, null, cancellationToken).ConfigureAwait(false);
        AnsiConsole.Clear();
        var reporter = new CompareReporter(options, settings);
        reporter.Render(result);
    }

    private static List<FileSystemWatcher> CreateWatchers(ComparisonOptions options, Action onEvent)
    {
        var watchers = new List<FileSystemWatcher>();
        watchers.Add(CreateWatcher(options.Left, onEvent));
        if (!string.IsNullOrWhiteSpace(options.Right))
        {
            watchers.Add(CreateWatcher(options.Right!, onEvent));
        }

        return watchers;
    }

    private static FileSystemWatcher CreateWatcher(string path, Action onEvent)
    {
        var fullPath = Path.GetFullPath(path);
        if (!Directory.Exists(fullPath))
        {
            throw new CliUsageException($"Directory '{fullPath}' does not exist.");
        }

        var watcher = new FileSystemWatcher(fullPath)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
        };

        FileSystemEventHandler handler = (_, _) => onEvent();
        RenamedEventHandler renameHandler = (_, _) => onEvent();
        ErrorEventHandler errorHandler = (_, _) => onEvent();

        watcher.Changed += handler;
        watcher.Created += handler;
        watcher.Deleted += handler;
        watcher.Renamed += renameHandler;
        watcher.Error += errorHandler;
        watcher.EnableRaisingEvents = true;
        return watcher;
    }
}
