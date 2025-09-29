using System;
using System.Collections.Immutable;
using System.IO;
using Spectre.Console;
using Spectre.Console.Cli;
using FsEqual.Core;
using FsEqual.Reporting;
using FsEqual.Interactive;

namespace FsEqual.Commands;

public sealed class CompareCommand : AsyncCommand<CompareCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<LEFT>")]
        public string Left { get; init; } = string.Empty;

        [CommandArgument(1, "<RIGHT>")]
        public string Right { get; init; } = string.Empty;

        [CommandOption("-t|--threads <INT>")]
        public int? Threads { get; init; }

        [CommandOption("-a|--algo <ALGO>")]
        public string Algorithm { get; init; } = "crc32";

        [CommandOption("-m|--mode <MODE>")]
        public string Mode { get; init; } = "quick";

        [CommandOption("-i|--ignore <GLOB>")]
        public string[] Ignore { get; init; } = Array.Empty<string>();

        [CommandOption("--case-sensitive")]
        public bool CaseSensitive { get; init; }

        [CommandOption("--follow-symlinks")]
        public bool FollowSymlinks { get; init; }

        [CommandOption("--mtime-tolerance <SECONDS>")]
        public double? MTimeTolerance { get; init; }

        [CommandOption("-v|--verbosity <LEVEL>")]
        public string Verbosity { get; init; } = "info";

        [CommandOption("--json <PATH>")]
        public string? JsonOutput { get; init; }

        [CommandOption("--summary <PATH>")]
        public string? SummaryOutput { get; init; }

        [CommandOption("--no-progress")]
        public bool NoProgress { get; init; }

        [CommandOption("--interactive")]
        public bool Interactive { get; init; }

        [CommandOption("--timeout <SECONDS>")]
        public int? TimeoutSeconds { get; init; }

        [CommandOption("--fail-on <KIND>")]
        public string FailOn { get; init; } = "diff";

        [CommandOption("--profile <NAME>")]
        public string? Profile { get; init; }

        [CommandOption("--config <PATH>")]
        public string? Config { get; init; }
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Left) || string.IsNullOrWhiteSpace(settings.Right))
        {
            return ValidationResult.Error("Both <LEFT> and <RIGHT> paths must be provided.");
        }

        if (!Directory.Exists(settings.Left))
        {
            return ValidationResult.Error($"Left path '{settings.Left}' does not exist.");
        }

        if (!Directory.Exists(settings.Right))
        {
            return ValidationResult.Error($"Right path '{settings.Right}' does not exist.");
        }

        if (settings.Threads.HasValue && settings.Threads.Value <= 0)
        {
            return ValidationResult.Error("--threads must be a positive integer.");
        }

        if (settings.TimeoutSeconds.HasValue && settings.TimeoutSeconds <= 0)
        {
            return ValidationResult.Error("--timeout must be positive.");
        }

        return ValidationResult.Success();
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var options = new ComparisonOptions
        {
            LeftRoot = Path.GetFullPath(settings.Left),
            RightRoot = Path.GetFullPath(settings.Right),
            Mode = ParseMode(settings.Mode),
            HashAlgorithm = ParseAlgorithm(settings.Algorithm),
            Threads = settings.Threads,
            CaseSensitive = settings.CaseSensitive,
            FollowSymlinks = settings.FollowSymlinks,
            MTimeTolerance = TimeSpan.FromSeconds(settings.MTimeTolerance ?? 0),
            IgnoreGlobs = settings.Ignore.ToImmutableArray(),
            NoProgress = settings.NoProgress || settings.Interactive,
            JsonOutputPath = settings.JsonOutput,
            SummaryOutputPath = settings.SummaryOutput,
            Interactive = settings.Interactive,
            TimeoutSeconds = settings.TimeoutSeconds,
            FailOn = ParseFailOn(settings.FailOn),
            ProfileName = settings.Profile,
            ConfigPath = settings.Config,
            Verbosity = ParseVerbosity(settings.Verbosity)
        };

        var service = new ComparisonService(options, AnsiConsole.Console);
        var result = await service.ExecuteAsync(CancellationToken.None);

        if (settings.Interactive)
        {
            var interactive = new InteractiveSession(options, result, AnsiConsole.Console);
            interactive.Run();
        }
        else
        {
            var reporter = new ConsoleReporter(AnsiConsole.Console, options.Verbosity);
            reporter.Render(result, options);
        }

        if (!settings.Interactive)
        {
            var exporter = new ReportExporter();
            await exporter.ExportAsync(result, options, AnsiConsole.Console);
        }

        var exitCode = DetermineExitCode(result, options.FailOn);
        return exitCode;
    }

    private static ComparisonMode ParseMode(string value)
    {
        var normalized = value.ToLowerInvariant();
        if (normalized == "quick")
        {
            return ComparisonMode.Quick;
        }
        if (normalized == "hash")
        {
            return ComparisonMode.Hash;
        }

        throw new InvalidOperationException($"Unknown comparison mode '{value}'.");
    }

    private static HashAlgorithmKind ParseAlgorithm(string value)
    {
        var normalized = value.ToLowerInvariant();
        return normalized switch
        {
            "crc32" => HashAlgorithmKind.Crc32,
            "md5" => HashAlgorithmKind.Md5,
            "sha256" => HashAlgorithmKind.Sha256,
            "xxh64" => HashAlgorithmKind.Xxh64,
            _ => throw new InvalidOperationException($"Unknown hash algorithm '{value}'.")
        };
    }

    private static FailOnCondition ParseFailOn(string value)
    {
        var normalized = value.ToLowerInvariant();
        return normalized switch
        {
            "any" => FailOnCondition.Any,
            "diff" => FailOnCondition.Diff,
            "error" => FailOnCondition.Error,
            _ => throw new InvalidOperationException($"Unknown fail-on option '{value}'.")
        };
    }

    private static VerbosityLevel ParseVerbosity(string value)
    {
        var normalized = value.ToLowerInvariant();
        return normalized switch
        {
            "trace" => VerbosityLevel.Trace,
            "debug" => VerbosityLevel.Debug,
            "info" => VerbosityLevel.Info,
            "warn" => VerbosityLevel.Warn,
            "error" => VerbosityLevel.Error,
            _ => throw new InvalidOperationException($"Unknown verbosity '{value}'.")
        };
    }

    private static int DetermineExitCode(ComparisonResult result, FailOnCondition failOn)
    {
        var hasErrors = result.Summary.Errors > 0 || result.Errors.Length > 0;
        var hasDifferences = result.Summary.DifferentFiles > 0 || result.Summary.MissingLeft > 0 || result.Summary.MissingRight > 0;

        if (hasErrors)
        {
            return 2;
        }

        if (failOn != FailOnCondition.Error && hasDifferences)
        {
            return 1;
        }

        return 0;
    }
}
