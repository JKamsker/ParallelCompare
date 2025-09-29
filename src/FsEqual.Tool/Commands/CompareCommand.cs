using System.ComponentModel;
using System.Linq;
using System.Threading;
using FsEqual.Tool.Configuration;
using FsEqual.Tool.Core;
using FsEqual.Tool.Interactive;
using FsEqual.Tool.Reporting;
using Spectre.Console;
using Spectre.Console.Cli;

namespace FsEqual.Tool.Commands;

internal sealed class CompareCommand : AsyncCommand<CompareCommand.Settings>
{
    private readonly ConfigLoader _configLoader = new();

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var cancellationToken = CancellationToken.None;
        ResolvedCompareSettings resolved;
        try
        {
            resolved = await settings.ResolveAsync(_configLoader, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            return -1;
        }

        var validationError = resolved.Validate();
        if (validationError is not null)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(validationError)}[/]");
            return -1;
        }

        using var timeoutCts = resolved.TimeoutSeconds.HasValue
            ? new CancellationTokenSource(TimeSpan.FromSeconds(resolved.TimeoutSeconds.Value))
            : null;

        using var linkedCts = timeoutCts is null
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var token = linkedCts.Token;

        var logger = new ConsoleLogger(resolved.Verbosity);
        var comparator = new DirectoryComparator(logger);
        var options = resolved.ToComparisonOptions();

        try
        {
            var result = await comparator.CompareAsync(options, !resolved.NoProgress && !resolved.Interactive, token);

            if (resolved.Interactive)
            {
                if (resolved.JsonOutput is not null || resolved.SummaryOutput is not null || resolved.NoProgress)
                {
                    logger.Log(VerbosityLevel.Warn, "Interactive mode ignores --json, --summary, and --no-progress options.");
                }

                var session = new InteractiveSession(result, resolved, logger);
                await session.RunAsync(token);
            }
            else
            {
                RenderSummary(result, resolved, logger);
                await ReportWriter.WriteReportsAsync(result, resolved, token);
            }

            return DetermineExitCode(resolved.FailOn, result);
        }
        catch (OperationCanceledException) when (timeoutCts?.IsCancellationRequested == true)
        {
            logger.Log(VerbosityLevel.Error, "Comparison timed out.");
            return (int)FailureExitCode.RuntimeError;
        }
    }

    private static void RenderSummary(ComparisonResult result, ResolvedCompareSettings settings, ConsoleLogger logger)
    {
        logger.Log(VerbosityLevel.Info, $"Compared '{settings.LeftPath}' to '{settings.RightPath}' in {result.Duration:g}.");

        var panel = new Panel(new Table()
            .AddColumn("Metric")
            .AddColumn("Value")
            .AddRow("Files Compared", result.Summary.FilesCompared.ToString())
            .AddRow("Directories Compared", result.Summary.DirectoriesCompared.ToString())
            .AddRow("Equal Files", result.Summary.EqualFiles.ToString())
            .AddRow("Equal Directories", result.Summary.EqualDirectories.ToString())
            .AddRow("Missing Left", result.Summary.MissingLeft.ToString())
            .AddRow("Missing Right", result.Summary.MissingRight.ToString())
            .AddRow("Type Mismatches", result.Summary.TypeMismatches.ToString())
            .AddRow("Size Mismatches", result.Summary.SizeMismatches.ToString())
            .AddRow("Hash Mismatches", result.Summary.HashMismatches.ToString())
            .AddRow("Metadata Mismatches", result.Summary.MetadataMismatches.ToString())
            .AddRow("Errors", result.Errors.Count.ToString()))
        {
            Header = new PanelHeader("Comparison Summary"),
        };

        AnsiConsole.Write(panel);

        if (result.Differences.Count > 0)
        {
            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumns("Type", "Path", "Detail");
            foreach (var diff in result.Differences.Take(50))
            {
                table.AddRow(diff.Type.ToString(), diff.RelativePath, diff.Detail ?? string.Empty);
            }

            if (result.Differences.Count > 50)
            {
                table.Caption($"Showing first 50 of {result.Differences.Count} differences");
            }

            AnsiConsole.Write(table);
        }

        if (result.Errors.Count > 0)
        {
            var errorTable = new Table().Border(TableBorder.Rounded);
            errorTable.AddColumns("Scope", "Message");
            foreach (var error in result.Errors.Take(20))
            {
                errorTable.AddRow(error.Scope, error.Message);
            }

            if (result.Errors.Count > 20)
            {
                errorTable.Caption($"Showing first 20 of {result.Errors.Count} errors");
            }

            AnsiConsole.Write(errorTable);
        }
    }

    private static int DetermineExitCode(FailureBehavior behavior, ComparisonResult result)
    {
        return behavior switch
        {
            FailureBehavior.Any when result.Outcome != ComparisonOutcome.Equal => (int)FailureExitCode.Differences,
            FailureBehavior.Difference when result.Outcome == ComparisonOutcome.Differences => (int)FailureExitCode.Differences,
            FailureBehavior.Difference when result.Outcome == ComparisonOutcome.Errors => (int)FailureExitCode.RuntimeError,
            FailureBehavior.Error when result.Outcome == ComparisonOutcome.Errors => (int)FailureExitCode.RuntimeError,
            _ => (int)FailureExitCode.Ok,
        };
    }

    private enum FailureExitCode
    {
        Ok = 0,
        Differences = 1,
        RuntimeError = 2,
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<left>")]
        [Description("Left directory to compare.")]
        public string LeftPath { get; set; } = string.Empty;

        [CommandArgument(1, "<right>")]
        [Description("Right directory to compare.")]
        public string RightPath { get; set; } = string.Empty;

        [CommandOption("-t|--threads <int>")]
        [Description("Maximum worker threads (default: number of logical processors).")]
        public int? Threads { get; set; }

        [CommandOption("-a|--algo <crc32|md5|sha256|xxh64>")]
        [Description("Hash algorithm for hash mode.")]
        public HashAlgorithmKind? Algorithm { get; set; }

        [CommandOption("-m|--mode <quick|hash>")]
        [Description("Comparison mode: quick (metadata) or hash (content).")]
        public ComparisonMode? Mode { get; set; }

        [CommandOption("-i|--ignore <glob>")]
        [Description("Glob patterns to ignore.")]
        public string[] Ignore { get; set; } = Array.Empty<string>();

        [CommandOption("--case-sensitive")]
        [Description("Enable case sensitive comparisons.")]
        public FlagValue<bool>? CaseSensitive { get; set; }

        [CommandOption("--follow-symlinks")]
        [Description("Follow symbolic links during traversal.")]
        public FlagValue<bool>? FollowSymlinks { get; set; }

        [CommandOption("--mtime-tolerance <seconds>")]
        [Description("Allowed modification time delta (seconds) in quick mode.")]
        public double? MtimeTolerance { get; set; }

        [CommandOption("-v|--verbosity <trace|debug|info|warn|error>")]
        [Description("Verbosity for console logging.")]
        public VerbosityLevel? Verbosity { get; set; }

        [CommandOption("--json <path>")]
        [Description("Write detailed JSON report to path.")]
        public string? JsonOutput { get; set; }

        [CommandOption("--summary <path>")]
        [Description("Write condensed JSON summary to path.")]
        public string? SummaryOutput { get; set; }

        [CommandOption("--no-progress")]
        [Description("Disable progress updates.")]
        public FlagValue<bool>? NoProgress { get; set; }

        [CommandOption("--interactive")]
        [Description("Start interactive TUI explorer after comparison.")]
        public FlagValue<bool>? Interactive { get; set; }

        [CommandOption("--timeout <seconds>")]
        [Description("Abort the comparison after the specified number of seconds.")]
        public int? TimeoutSeconds { get; set; }

        [CommandOption("--fail-on <any|diff|error>")]
        [Description("Choose which outcomes should produce a failing exit code.")]
        public FailureBehavior? FailOn { get; set; }

        [CommandOption("--profile <name>")]
        [Description("Profile name from configuration file.")]
        public string? Profile { get; set; }

        [CommandOption("--config <path>")]
        [Description("Path to configuration file (defaults to fsequal.config.json).")]
        public string? ConfigPath { get; set; }

        internal async Task<ResolvedCompareSettings> ResolveAsync(ConfigLoader loader, CancellationToken token)
        {
            var config = await loader.LoadAsync(ConfigPath, token);
            CompareProfile? defaults = config?.Defaults;
            CompareProfile? profile = null;

            if (!string.IsNullOrWhiteSpace(Profile))
            {
                if (config?.Profiles == null || !config.Profiles.TryGetValue(Profile!, out profile))
                {
                    throw new InvalidOperationException($"Profile '{Profile}' not found.");
                }
            }

            var ignore = new List<string>();
            AppendIgnore(ignore, defaults?.Ignore);
            AppendIgnore(ignore, profile?.Ignore);
            AppendIgnore(ignore, Ignore);

            var caseSensitive = CaseSensitive is { IsSet: true } cs ? cs.Value : profile?.CaseSensitive ?? defaults?.CaseSensitive ?? false;
            var followSymlinks = FollowSymlinks is { IsSet: true } fs ? fs.Value : profile?.FollowSymlinks ?? defaults?.FollowSymlinks ?? false;
            var noProgress = NoProgress is { IsSet: true } np ? np.Value : profile?.NoProgress ?? defaults?.NoProgress ?? false;
            var interactive = Interactive is { IsSet: true } iv ? iv.Value : profile?.Interactive ?? defaults?.Interactive ?? false;

            var mode = Mode
                ?? ParseEnum<ComparisonMode>(profile?.Mode)
                ?? ParseEnum<ComparisonMode>(defaults?.Mode)
                ?? ComparisonMode.Quick;

            var algorithm = Algorithm
                ?? ParseEnum<HashAlgorithmKind>(profile?.Algorithm)
                ?? ParseEnum<HashAlgorithmKind>(defaults?.Algorithm)
                ?? HashAlgorithmKind.Crc32;

            var threads = Threads ?? profile?.Threads ?? defaults?.Threads ?? Environment.ProcessorCount;
            var tolerance = MtimeTolerance ?? profile?.MtimeTolerance ?? defaults?.MtimeTolerance ?? 0d;
            var verbosity = Verbosity
                ?? ParseEnum<VerbosityLevel>(profile?.Verbosity)
                ?? ParseEnum<VerbosityLevel>(defaults?.Verbosity)
                ?? VerbosityLevel.Info;
            var failBehavior = FailOn
                ?? ParseEnum<FailureBehavior>(profile?.FailOn)
                ?? ParseEnum<FailureBehavior>(defaults?.FailOn)
                ?? FailureBehavior.Difference;
            var timeout = TimeoutSeconds ?? profile?.TimeoutSeconds ?? defaults?.TimeoutSeconds;

            var ignoreList = ignore
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new ResolvedCompareSettings
            {
                LeftPath = LeftPath,
                RightPath = RightPath,
                Mode = mode,
                Algorithm = algorithm,
                Threads = Math.Max(1, threads),
                IgnoreGlobs = ignoreList,
                CaseSensitive = caseSensitive,
                FollowSymlinks = followSymlinks,
                MtimeToleranceSeconds = Math.Max(0, tolerance),
                Verbosity = verbosity,
                JsonOutput = string.IsNullOrWhiteSpace(JsonOutput) ? null : JsonOutput,
                SummaryOutput = string.IsNullOrWhiteSpace(SummaryOutput) ? null : SummaryOutput,
                NoProgress = noProgress,
                Interactive = interactive,
                FailOn = failBehavior,
                TimeoutSeconds = timeout,
                ConfigPath = config?.SourcePath ?? ConfigPath,
                Profile = Profile,
            };
        }

        private static void AppendIgnore(List<string> destination, IEnumerable<string>? values)
        {
            if (values == null)
            {
                return;
            }

            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    destination.Add(value);
                }
            }
        }

        private static TEnum? ParseEnum<TEnum>(string? value)
            where TEnum : struct
        {
            if (!string.IsNullOrWhiteSpace(value) && Enum.TryParse<TEnum>(value, true, out var result))
            {
                return result;
            }

            return null;
        }
    }
}
