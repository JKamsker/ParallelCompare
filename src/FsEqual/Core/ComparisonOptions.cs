using System.Collections.Immutable;

namespace FsEqual.Core;

public sealed record ComparisonOptions
{
    public required string LeftRoot { get; init; }
    public required string RightRoot { get; init; }
    public ComparisonMode Mode { get; init; } = ComparisonMode.Quick;
    public HashAlgorithmKind HashAlgorithm { get; init; } = HashAlgorithmKind.Crc32;
    public int? Threads { get; init; }
    public bool CaseSensitive { get; init; }
    public bool FollowSymlinks { get; init; }
    public TimeSpan MTimeTolerance { get; init; } = TimeSpan.Zero;
    public ImmutableArray<string> IgnoreGlobs { get; init; } = ImmutableArray<string>.Empty;
    public bool NoProgress { get; init; }
    public string? JsonOutputPath { get; init; }
    public string? SummaryOutputPath { get; init; }
    public bool Interactive { get; init; }
    public int? TimeoutSeconds { get; init; }
    public FailOnCondition FailOn { get; init; } = FailOnCondition.Diff;
    public string? ProfileName { get; init; }
    public string? ConfigPath { get; init; }
    public VerbosityLevel Verbosity { get; init; } = VerbosityLevel.Info;
}

public enum VerbosityLevel
{
    Trace,
    Debug,
    Info,
    Warn,
    Error
}
