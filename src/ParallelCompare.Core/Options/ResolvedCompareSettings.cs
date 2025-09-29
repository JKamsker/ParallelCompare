using System.Collections.Immutable;
using ParallelCompare.Core.Comparison;
using ParallelCompare.Core.FileSystem;

namespace ParallelCompare.Core.Options;

public sealed record ResolvedCompareSettings
{
    public required string LeftPath { get; init; }
    public string? RightPath { get; init; }
    public ComparisonMode Mode { get; init; }
    public ImmutableArray<HashAlgorithmType> Algorithms { get; init; } = ImmutableArray<HashAlgorithmType>.Empty;
    public ImmutableArray<string> IgnorePatterns { get; init; } = ImmutableArray<string>.Empty;
    public bool CaseSensitive { get; init; }
    public bool FollowSymlinks { get; init; }
    public TimeSpan? ModifiedTimeTolerance { get; init; }
    public int? Threads { get; init; }
    public string? BaselinePath { get; init; }
    public string? JsonReportPath { get; init; }
    public string? SummaryReportPath { get; init; }
    public string? ExportFormat { get; init; }
    public bool NoProgress { get; init; }
    public string? DiffTool { get; init; }
    public string? Verbosity { get; init; }
    public string? FailOn { get; init; }
    public TimeSpan? Timeout { get; init; }
    public string? InteractiveTheme { get; init; }
    public string? InteractiveFilter { get; init; }
    public string? InteractiveVerbosity { get; init; }
    public bool UsesBaseline { get; init; }
    public BaselineMetadata? BaselineMetadata { get; init; }
    public IFileSystem FileSystem { get; init; } = PhysicalFileSystem.Instance;
}
