using System.Collections.Immutable;
using ParallelCompare.Core.Comparison;
using ParallelCompare.Core.FileSystem;

namespace ParallelCompare.Core.Options;

public sealed record ComparisonOptions
{
    public required string LeftPath { get; init; }
    public required string RightPath { get; init; }
    public ComparisonMode Mode { get; init; } = ComparisonMode.Quick;
    public ImmutableArray<HashAlgorithmType> HashAlgorithms { get; init; } = ImmutableArray<HashAlgorithmType>.Empty;
    public ImmutableArray<string> IgnorePatterns { get; init; } = ImmutableArray<string>.Empty;
    public bool CaseSensitive { get; init; }
    public bool FollowSymlinks { get; init; }
    public TimeSpan? ModifiedTimeTolerance { get; init; }
    public int? MaxDegreeOfParallelism { get; init; }
    public string? BaselinePath { get; init; }
    public bool EnableInteractive { get; init; }
    public string? JsonReportPath { get; init; }
    public string? SummaryReportPath { get; init; }
    public string? ExportFormat { get; init; }
    public bool NoProgress { get; init; }
    public string? DiffTool { get; init; }
    public IComparisonUpdateSink? UpdateSink { get; init; }
    public CancellationToken CancellationToken { get; init; } = CancellationToken.None;
    public IFileSystem FileSystem { get; init; } = PhysicalFileSystem.Instance;
}
