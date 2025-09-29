namespace FsEqual.Tool.Core;

internal sealed class ComparisonOptions
{
    public required string LeftRoot { get; init; }

    public required string RightRoot { get; init; }

    public required ComparisonMode Mode { get; init; }

    public required HashAlgorithmKind Algorithm { get; init; }

    public required int MaxDegreeOfParallelism { get; init; }

    public required IReadOnlyList<string> IgnoreGlobs { get; init; }

    public required bool CaseSensitive { get; init; }

    public required bool FollowSymlinks { get; init; }

    public required double MtimeToleranceSeconds { get; init; }
}
