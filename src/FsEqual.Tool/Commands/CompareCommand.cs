using FsEqual.Tool.Models;
using FsEqual.Tool.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace FsEqual.Tool.Commands;

public sealed class CompareCommand : AsyncCommand<CompareCommand.Settings>
{
    private readonly DirectoryEnumerator _enumerator = new();
    private readonly DirectoryComparator _comparator = new();
    private readonly SnapshotService _snapshotService = new();
    private readonly ConfigurationLoader _configLoader = new();
    private readonly ReportExporter _exporter = new();

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<LEFT>")]
        public string Left { get; set; } = string.Empty;

        [CommandArgument(1, "[RIGHT]")]
        public string? Right { get; set; }

        [CommandOption("-t|--threads <THREADS>")]
        public int Threads { get; set; }

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

        [CommandOption("-v|--verbosity <LEVEL>")]
        public string Verbosity { get; set; } = "info";

        [CommandOption("--json <PATH>")]
        public string? Json { get; set; }

        [CommandOption("--summary <PATH>")]
        public string? Summary { get; set; }

        [CommandOption("--no-progress")]
        public bool NoProgress { get; set; }

        [CommandOption("--interactive")]
        public bool Interactive { get; set; }

        [CommandOption("--timeout <SECONDS>")]
        public int? Timeout { get; set; }

        [CommandOption("--fail-on <VALUE>")]
        public string FailOn { get; set; } = "diff";

        [CommandOption("--profile <NAME>")]
        public string? Profile { get; set; }

        [CommandOption("--config <FILE>")]
        public string? Config { get; set; }

        [CommandOption("--baseline <FILE>")]
        public string? Baseline { get; set; }
    }

    private enum FailCondition
    {
        Any,
        Diff,
        Error,
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Left))
        {
            return ValidationResult.Error("Left path is required.");
        }

        if (settings.Right == null && settings.Baseline == null)
        {
            return ValidationResult.Error("Either a right path or a baseline must be provided.");
        }

        return ValidationResult.Success();
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        using var timeoutCts = settings.Timeout.HasValue
            ? new CancellationTokenSource(TimeSpan.FromSeconds(settings.Timeout.Value))
            : new CancellationTokenSource();

        void OnCancel(object? sender, ConsoleCancelEventArgs args)
        {
            args.Cancel = true;
            timeoutCts.Cancel();
        }

        Console.CancelKeyPress += OnCancel;

        try
        {
            var cancellationToken = timeoutCts.Token;

        ConfigurationLoader.ProfileSettings? profile = null;
        if (!string.IsNullOrWhiteSpace(settings.Config))
        {
            profile = await _configLoader.LoadAsync(settings.Config, settings.Profile, cancellationToken);
        }

        var algorithmName = !string.IsNullOrWhiteSpace(settings.Algorithm) ? settings.Algorithm : profile?.Algo ?? "crc32";
        var modeName = !string.IsNullOrWhiteSpace(settings.Mode) ? settings.Mode : profile?.Mode ?? "quick";
        var algorithm = ParseAlgorithm(algorithmName);
        var mode = ParseMode(modeName);
        var threads = settings.Threads > 0 ? settings.Threads : profile?.Threads ?? Environment.ProcessorCount;
        var caseSensitive = settings.CaseSensitive || (profile?.CaseSensitive ?? false);
        var followSymlinks = settings.FollowSymlinks || (profile?.FollowSymlinks ?? false);
        var mtimeTolerance = settings.MtimeTolerance ?? profile?.MtimeTolerance;
        var noProgress = settings.NoProgress || (profile?.NoProgress ?? false);
        var jsonPath = settings.Json ?? profile?.Json;
        var summaryPath = settings.Summary ?? profile?.Summary;
        var failCondition = ParseFailCondition(settings.FailOn);

        var ignore = new List<string>();
        if (profile != null)
        {
            ignore.AddRange(profile.Ignore);
        }

        if (settings.Ignore.Length > 0)
        {
            ignore.AddRange(settings.Ignore);
        }

        var options = new ComparisonOptions
        {
            Algorithm = algorithm,
            Mode = mode,
            Threads = threads,
            CaseSensitive = caseSensitive,
            FollowSymlinks = followSymlinks,
            MtimeToleranceSeconds = mtimeTolerance,
            IgnoreGlobs = ignore,
            NoProgress = noProgress,
        };

        DirectorySnapshot? leftSnapshot = null;
        DirectorySnapshot? rightSnapshot = null;

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Star)
            .StartAsync("Scanning directories...", async ctx =>
            {
                ctx.Status = "Scanning left directory";
                leftSnapshot = await _enumerator.CaptureAsync(settings.Left, options, cancellationToken);

                if (!string.IsNullOrWhiteSpace(settings.Baseline))
                {
                    ctx.Status = "Loading baseline";
                    rightSnapshot = await _snapshotService.LoadAsync(settings.Baseline!, cancellationToken);
                }
                else
                {
                    if (settings.Right == null)
                    {
                        throw new InvalidOperationException("Right path is required when baseline is not provided.");
                    }

                    ctx.Status = "Scanning right directory";
                    rightSnapshot = await _enumerator.CaptureAsync(settings.Right!, options, cancellationToken);
                }
            });

        if (leftSnapshot == null)
        {
            throw new InvalidOperationException("Failed to enumerate left directory.");
        }

        ComparisonReport report;
        if (options.NoProgress || settings.Interactive)
        {
            report = await _comparator.CompareAsync(leftSnapshot, rightSnapshot, options, algorithm, cancellationToken);
        }
        else
        {
            report = await RenderProgressComparison(leftSnapshot, rightSnapshot, options, algorithm, cancellationToken);
        }

        if (!settings.Interactive)
        {
            RenderSummary(report, settings.Verbosity);
        }

        if (!string.IsNullOrEmpty(jsonPath))
        {
            await _exporter.ExportFullAsync(report, jsonPath!, cancellationToken);
            AnsiConsole.MarkupLine($"[green]JSON report written to {jsonPath}[/]");
        }

        if (!string.IsNullOrEmpty(summaryPath))
        {
            await _exporter.ExportSummaryAsync(report, summaryPath!, cancellationToken);
            AnsiConsole.MarkupLine($"[green]Summary report written to {summaryPath}[/]");
        }

        if (settings.Interactive)
        {
            var explorer = new Interactive.InteractiveExplorer();
            await explorer.RunAsync(report, cancellationToken);
        }

        return DetermineExitCode(report, failCondition);
        }
        finally
        {
            Console.CancelKeyPress -= OnCancel;
        }
    }

    private async Task<ComparisonReport> RenderProgressComparison(
        DirectorySnapshot left,
        DirectorySnapshot? right,
        ComparisonOptions options,
        HashAlgorithmKind algorithm,
        CancellationToken cancellationToken)
    {
        return await AnsiConsole.Progress()
            .AutoClear(false)
            .Columns(new ProgressColumn[]
            {
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
            })
            .StartAsync(async ctx =>
            {
                var hashTask = ctx.AddTask("Hashing", autoStart: false);
                var compareTask = ctx.AddTask("Comparing", autoStart: true);

                var reporter = new Progress<(string Stage, int Value, int Total)>(tuple =>
                {
                    if (tuple.Stage == "hash")
                    {
                        if (tuple.Total == 0)
                        {
                            hashTask.MaxValue = 1;
                            hashTask.Value = 1;
                        }
                        else
                        {
                            hashTask.MaxValue = tuple.Total;
                            hashTask.StartTask();
                            hashTask.Value = tuple.Value;
                        }
                    }
                    else if (tuple.Stage == "files")
                    {
                        compareTask.MaxValue = tuple.Total == 0 ? 1 : tuple.Total;
                        compareTask.Value = tuple.Value;
                    }
                });

                var report = await _comparator.CompareAsync(left, right, options, algorithm, cancellationToken, reporter);
                hashTask.StopTask();
                compareTask.StopTask();
                return report;
            });
    }

    private static void RenderSummary(ComparisonReport report, string verbosity)
    {
        var panel = new Panel(new Rows(
            new Markup($"[bold]Left:[/] {report.LeftRoot}" + (report.RightRoot != null ? $"\n[bold]Right:[/] {report.RightRoot}" : string.Empty)),
            new Rule(),
            new Markup($"Mode: {report.Mode} | Algo: {report.Algorithm} | Duration: {report.Summary.Duration}")
        ))
        {
            Header = new PanelHeader("fsEqual"),
        };

        AnsiConsole.Write(panel);

        if (report.Summary.Differences > 0)
        {
            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("Status");
            table.AddColumn("Path");
            table.AddColumn("Reason");

            foreach (var item in report.Items.Where(i => i.Status != ComparisonStatus.Equal).Take(25))
            {
                table.AddRow(item.Status.ToString(), item.RelativePath, item.Reason ?? string.Empty);
            }

            if (report.Items.Count(i => i.Status != ComparisonStatus.Equal) > 25)
            {
                table.Caption = new TableTitle("Showing first 25 differences");
            }

            AnsiConsole.Write(table);
        }
        else
        {
            AnsiConsole.MarkupLine("[green]Directories are identical.[/]");
        }

        if (report.Errors.Count > 0)
        {
            var warn = new Table().Border(TableBorder.None);
            warn.AddColumn("Errors");
            foreach (var error in report.Errors.Take(10))
            {
                warn.AddRow($"{error.Path}: {error.Message}");
            }

            if (report.Errors.Count > 10)
            {
                warn.Caption = new TableTitle("Showing first 10 errors");
            }

            AnsiConsole.Write(warn);
        }
    }

    private static FailCondition ParseFailCondition(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "any" => FailCondition.Any,
            "diff" => FailCondition.Diff,
            "error" => FailCondition.Error,
            _ => FailCondition.Diff,
        };
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

    private static int DetermineExitCode(ComparisonReport report, FailCondition condition)
    {
        var hasDifferences = report.Summary.Differences > 0;
        var hasErrors = report.Errors.Count > 0;

        return condition switch
        {
            FailCondition.Any => hasDifferences || hasErrors ? 1 : 0,
            FailCondition.Diff => hasDifferences ? 1 : 0,
            FailCondition.Error => hasErrors ? 1 : 0,
            _ => hasDifferences ? 1 : 0,
        };
    }
}
