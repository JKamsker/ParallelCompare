using System.Collections.Immutable;
using ParallelCompare.Core.Options;

namespace ParallelCompare.Core.Comparison;

public enum ComparisonNodeType
{
    Directory,
    File
}

public enum ComparisonStatus
{
    Equal,
    Different,
    LeftOnly,
    RightOnly,
    Error
}

public sealed record FileComparisonDetail(
    long? LeftSize,
    long? RightSize,
    DateTimeOffset? LeftModified,
    DateTimeOffset? RightModified,
    IReadOnlyDictionary<HashAlgorithmType, string>? LeftHashes,
    IReadOnlyDictionary<HashAlgorithmType, string>? RightHashes,
    string? ErrorMessage
);

public sealed record ComparisonNode(
    string Name,
    string RelativePath,
    ComparisonNodeType NodeType,
    ComparisonStatus Status,
    FileComparisonDetail? Detail,
    ImmutableArray<ComparisonNode> Children
)
{
    public bool HasDifferences => Status is ComparisonStatus.Different or ComparisonStatus.LeftOnly or ComparisonStatus.RightOnly or ComparisonStatus.Error;
}

public sealed record ComparisonSummary(
    int TotalFiles,
    int EqualFiles,
    int DifferentFiles,
    int LeftOnlyFiles,
    int RightOnlyFiles,
    int ErrorFiles
);

public sealed record ComparisonResult(
    string LeftPath,
    string RightPath,
    ComparisonNode Root,
    ComparisonSummary Summary,
    BaselineMetadata? Baseline = null
);

public sealed record BaselineMetadata(
    string ManifestPath,
    string SourcePath,
    DateTimeOffset CreatedAt,
    ImmutableArray<HashAlgorithmType> Algorithms
);
