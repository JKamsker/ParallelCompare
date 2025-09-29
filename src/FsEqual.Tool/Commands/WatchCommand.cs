using System.Collections.Concurrent;
using FsEqual.Tool.Models;
using FsEqual.Tool.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace FsEqual.Tool.Commands;

public sealed class WatchCommand : AsyncCommand<WatchCommand.Settings>
{
    private readonly DirectoryEnumerator _enumerator = new();
    private readonly DirectoryComparator _comparator = new();

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<LEFT>")]
        public string Left { get; set; } = string.Empty;

        [CommandArgument(1, "<RIGHT>")]
        public string Right { get; set; } = string.Empty;

        [CommandOption("-a|--algo <ALGO>")]
        public string Algorithm { get; set; } = "crc32";

        [CommandOption("-m|--mode <MODE>")]
        public string Mode { get; set; } = "quick";

        [CommandOption("-i|--ignore <GLOB>")]
        public string[] Ignore { get; set; } = Array.Empty<string>();

        [CommandOption("--case-sensitive")]
        public bool CaseSensitive { get; set; }

        [CommandOption("--follow-symlinks")]
        public bool FollowSymlinks { get; set; }

        [CommandOption("--mtime-tolerance <SECONDS>")]
        public double? MtimeTolerance { get; set; }

        [CommandOption("-t|--threads <THREADS>")]
        public int Threads { get; set; }

        [CommandOption("--debounce <MS>")]
        public int DebounceMilliseconds { get; set; } = 500;
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Left) || string.IsNullOrWhiteSpace(settings.Right))
        {
            return ValidationResult.Error("Both left and right paths are required.");
        }

        return ValidationResult.Success();
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        using var cts = new CancellationTokenSource();

        void OnCancel(object? sender, ConsoleCancelEventArgs args)
        {
            args.Cancel = true;
            cts.Cancel();
        }

        Console.CancelKeyPress += OnCancel;

        int exitCode = 0;

        try
        {
            var cancellationToken = cts.Token;
            var algorithm = ParseAlgorithm(settings.Algorithm);
            var mode = ParseMode(settings.Mode);
            var options = new ComparisonOptions
            {
                Algorithm = algorithm,
                Mode = mode,
                IgnoreGlobs = settings.Ignore,
                CaseSensitive = settings.CaseSensitive,
                FollowSymlinks = settings.FollowSymlinks,
                MtimeToleranceSeconds = settings.MtimeTolerance,
                Threads = settings.Threads > 0 ? settings.Threads : Environment.ProcessorCount,
                NoProgress = true,
            };

            await RunComparison(settings.Left, settings.Right, options, algorithm, cancellationToken);

            using var leftWatcher = CreateWatcher(settings.Left);
            using var rightWatcher = CreateWatcher(settings.Right);
            var channel = new BlockingCollection<bool>();

        void OnChange(object? sender, FileSystemEventArgs args)
        {
            channel.TryAdd(true);
        }

        void OnRename(object? sender, RenamedEventArgs args)
        {
            channel.TryAdd(true);
        }

        leftWatcher.Changed += OnChange;
        leftWatcher.Created += OnChange;
        leftWatcher.Deleted += OnChange;
        leftWatcher.Renamed += OnRename;
        leftWatcher.EnableRaisingEvents = true;

        rightWatcher.Changed += OnChange;
        rightWatcher.Created += OnChange;
        rightWatcher.Deleted += OnChange;
        rightWatcher.Renamed += OnRename;
        rightWatcher.EnableRaisingEvents = true;

        AnsiConsole.MarkupLine("Watching for changes. Press Ctrl+C to exit.");

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        channel.Take(cancellationToken);
                        await Task.Delay(settings.DebounceMilliseconds, cancellationToken);
                        while (channel.TryTake(out _))
                        {
                            // drain additional events
                        }

                        await RunComparison(settings.Left, settings.Right, options, algorithm, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
            finally
            {
                channel.Dispose();
                leftWatcher.Changed -= OnChange;
                leftWatcher.Created -= OnChange;
                leftWatcher.Deleted -= OnChange;
                leftWatcher.Renamed -= OnRename;
                rightWatcher.Changed -= OnChange;
                rightWatcher.Created -= OnChange;
                rightWatcher.Deleted -= OnChange;
                rightWatcher.Renamed -= OnRename;
            }
        }
        finally
        {
            Console.CancelKeyPress -= OnCancel;
        }

        return exitCode;
    }

    private async Task RunComparison(string left, string right, ComparisonOptions options, HashAlgorithmKind algorithm, CancellationToken cancellationToken)
    {
        var leftSnapshot = await _enumerator.CaptureAsync(left, options, cancellationToken);
        var rightSnapshot = await _enumerator.CaptureAsync(right, options, cancellationToken);
        var report = await _comparator.CompareAsync(leftSnapshot, rightSnapshot, options, algorithm, cancellationToken);

        AnsiConsole.Clear();
        AnsiConsole.MarkupLine($"[bold]Watching[/] {left} <-> {right}");
        AnsiConsole.MarkupLine($"Last run: {DateTimeOffset.Now:u}");
        AnsiConsole.MarkupLine($"Differences: {report.Summary.Differences}, Errors: {report.Errors.Count}");
        if (report.Summary.Differences > 0)
        {
            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("Status");
            table.AddColumn("Path");
            foreach (var diff in report.Items.Where(i => i.Status != ComparisonStatus.Equal).Take(15))
            {
                table.AddRow(diff.Status.ToString(), diff.RelativePath);
            }

            if (report.Items.Count(i => i.Status != ComparisonStatus.Equal) > 15)
            {
                table.Caption = new TableTitle("Showing first 15 differences");
            }

            AnsiConsole.Write(table);
        }
    }

    private static FileSystemWatcher CreateWatcher(string path)
    {
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"Directory '{path}' not found.");
        }

        var watcher = new FileSystemWatcher(path)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
        };

        return watcher;
    }

    private static HashAlgorithmKind ParseAlgorithm(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "crc32" => HashAlgorithmKind.Crc32,
            "md5" => HashAlgorithmKind.Md5,
            "sha256" => HashAlgorithmKind.Sha256,
            "xxh64" => HashAlgorithmKind.Xxh64,
            _ => throw new InvalidOperationException($"Unsupported algorithm '{value}'."),
        };
    }

    private static ComparisonMode ParseMode(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "quick" => ComparisonMode.Quick,
            "hash" => ComparisonMode.Hash,
            _ => throw new InvalidOperationException($"Unsupported comparison mode '{value}'."),
        };
    }
}
