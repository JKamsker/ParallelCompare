using System.Collections.Immutable;

namespace ParallelCompare.Core.Options;

public sealed record CompareSettingsInput
{
    public required string LeftPath { get; init; }
    public string? RightPath { get; init; }
    public string? Mode { get; init; }
    public string? Algorithm { get; init; }
    public ImmutableArray<string> AdditionalAlgorithms { get; init; } = ImmutableArray<string>.Empty;
    public ImmutableArray<string> IgnorePatterns { get; init; } = ImmutableArray<string>.Empty;
    public bool? CaseSensitive { get; init; }
    public bool? FollowSymlinks { get; init; }
    public TimeSpan? ModifiedTimeTolerance { get; init; }
    public int? Threads { get; init; }
    public string? BaselinePath { get; init; }
    public bool EnableInteractive { get; init; }
    public string? JsonReportPath { get; init; }
    public string? SummaryReportPath { get; init; }
    public string? ExportFormat { get; init; }
    public bool NoProgress { get; init; }
    public string? DiffTool { get; init; }
    public string? Profile { get; init; }
    public string? ConfigurationPath { get; init; }
    public string? Verbosity { get; init; }
    public string? FailOn { get; init; }
    public TimeSpan? Timeout { get; init; }
    public string? InteractiveTheme { get; init; }
    public string? InteractiveFilter { get; init; }
    public string? InteractiveVerbosity { get; init; }
}
