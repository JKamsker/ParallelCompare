using FsEqual.Tool.Comparison;
using Spectre.Console.Cli;

namespace FsEqual.Tool.Commands;

public abstract class CompareSettingsBase : CommandSettings
{
    [CommandArgument(0, "<LEFT>")]
    public required string Left { get; init; }

    [CommandArgument(1, "[RIGHT]")]
    public string? Right { get; init; }

    [CommandOption("-t|--threads <THREADS>")]
    public int? Threads { get; init; }

    [CommandOption("-a|--algo <ALGO>")]
    public string? Algorithm { get; init; }

    [CommandOption("-m|--mode <MODE>")]
    public string? Mode { get; init; }

    [CommandOption("-i|--ignore <GLOB>")]
    public string[]? Ignore { get; init; }

    [CommandOption("--case-sensitive")]
    public bool CaseSensitive { get; init; }

    [CommandOption("--follow-symlinks")]
    public bool FollowSymlinks { get; init; }

    [CommandOption("--mtime-tolerance <SECONDS>")]
    public double? MtimeTolerance { get; init; }

    [CommandOption("-v|--verbosity <LEVEL>")]
    public string? Verbosity { get; init; }

    [CommandOption("--timeout <SECONDS>")]
    public int? TimeoutSeconds { get; init; }

    [CommandOption("--fail-on <RULE>")]
    public string? FailOn { get; init; }

    [CommandOption("--profile <NAME>")]
    public string? Profile { get; init; }

    [CommandOption("--config <PATH>")]
    public string? ConfigPath { get; init; }

    protected virtual string? GetBaselinePath() => null;

    public ComparisonOptions ToOptions()
    {
        var baselinePath = GetBaselinePath();
        var right = string.IsNullOrWhiteSpace(Right) ? null : Right;

        if (right is null && string.IsNullOrWhiteSpace(baselinePath))
        {
            throw new CliUsageException("You must provide a right directory or --baseline path.");
        }

        return new ComparisonOptions
        {
            Left = Left,
            Right = right,
            MaxDegreeOfParallelism = Threads ?? Environment.ProcessorCount,
            Mode = ParseMode(Mode),
            Algorithm = ParseAlgorithm(Algorithm),
            Ignore = Ignore ?? Array.Empty<string>(),
            CaseSensitive = CaseSensitive,
            FollowSymlinks = FollowSymlinks,
            MtimeTolerance = MtimeTolerance != null ? TimeSpan.FromSeconds(MtimeTolerance.Value) : null,
            Verbosity = ParseVerbosity(Verbosity),
            Timeout = TimeoutSeconds != null ? TimeSpan.FromSeconds(Math.Max(1, TimeoutSeconds.Value)) : null,
            FailOn = ParseFailOn(FailOn),
            Profile = Profile,
            ConfigPath = ConfigPath,
            BaselinePath = string.IsNullOrWhiteSpace(baselinePath) ? null : baselinePath
        };
    }

    private static ComparisonMode ParseMode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ComparisonMode.Quick;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "quick" => ComparisonMode.Quick,
            "hash" => ComparisonMode.Hash,
            _ => throw new CliUsageException($"Unknown comparison mode '{value}'. Use 'quick' or 'hash'.")
        };
    }

    private static HashAlgorithmKind ParseAlgorithm(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return HashAlgorithmKind.Crc32;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "crc32" => HashAlgorithmKind.Crc32,
            "md5" => HashAlgorithmKind.Md5,
            "sha256" => HashAlgorithmKind.Sha256,
            "xxh64" => HashAlgorithmKind.Xxh64,
            _ => throw new CliUsageException($"Unknown hash algorithm '{value}'.")
        };
    }

    private static VerbosityLevel ParseVerbosity(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return VerbosityLevel.Info;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "trace" => VerbosityLevel.Trace,
            "debug" => VerbosityLevel.Debug,
            "info" => VerbosityLevel.Info,
            "warn" or "warning" => VerbosityLevel.Warn,
            "error" => VerbosityLevel.Error,
            _ => throw new CliUsageException($"Unknown verbosity '{value}'.")
        };
    }

    private static FailOnRule ParseFailOn(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return FailOnRule.Differences;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "any" => FailOnRule.Any,
            "diff" or "differences" => FailOnRule.Differences,
            "error" or "errors" => FailOnRule.Errors,
            _ => throw new CliUsageException($"Unknown fail rule '{value}'.")
        };
    }
}
