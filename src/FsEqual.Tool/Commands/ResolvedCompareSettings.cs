using System.IO;
using FsEqual.Tool.Core;

namespace FsEqual.Tool.Commands;

internal sealed class ResolvedCompareSettings
{
    public required string LeftPath { get; init; }

    public required string RightPath { get; init; }

    public required ComparisonMode Mode { get; init; }

    public required HashAlgorithmKind Algorithm { get; init; }

    public required int Threads { get; init; }

    public required IReadOnlyList<string> IgnoreGlobs { get; init; }

    public required bool CaseSensitive { get; init; }

    public required bool FollowSymlinks { get; init; }

    public required double MtimeToleranceSeconds { get; init; }

    public required VerbosityLevel Verbosity { get; init; }

    public required bool Interactive { get; init; }

    public required bool NoProgress { get; init; }

    public string? JsonOutput { get; init; }

    public string? SummaryOutput { get; init; }

    public required FailureBehavior FailOn { get; init; }

    public int? TimeoutSeconds { get; init; }

    public string? Profile { get; init; }

    public string? ConfigPath { get; init; }

    public string? Validate()
    {
        if (string.IsNullOrWhiteSpace(LeftPath) || !Directory.Exists(LeftPath))
        {
            return $"Left path '{LeftPath}' does not exist.";
        }

        if (string.IsNullOrWhiteSpace(RightPath) || !Directory.Exists(RightPath))
        {
            return $"Right path '{RightPath}' does not exist.";
        }

        if (TimeoutSeconds.HasValue && TimeoutSeconds <= 0)
        {
            return "Timeout must be positive.";
        }

        if (Threads <= 0)
        {
            return "Threads must be at least 1.";
        }

        return null;
    }

    public ComparisonOptions ToComparisonOptions()
    {
        return new ComparisonOptions
        {
            LeftRoot = Path.GetFullPath(LeftPath),
            RightRoot = Path.GetFullPath(RightPath),
            Mode = Mode,
            Algorithm = Algorithm,
            MaxDegreeOfParallelism = Threads,
            IgnoreGlobs = IgnoreGlobs,
            CaseSensitive = CaseSensitive,
            FollowSymlinks = FollowSymlinks,
            MtimeToleranceSeconds = MtimeToleranceSeconds,
        };
    }
}
