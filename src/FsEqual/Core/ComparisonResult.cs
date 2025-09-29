using System.Collections.Immutable;

namespace FsEqual.Core;

public sealed record ComparisonResult
{
    public required ComparisonSummary Summary { get; init; }
    public required ImmutableArray<DifferenceRecord> Differences { get; init; }
    public required ComparisonNode RootNode { get; init; }
    public required ImmutableArray<string> Errors { get; init; }
    public TimeSpan Elapsed { get; init; }
}

public sealed record ComparisonSummary
{
    public int TotalFiles { get; init; }
    public int TotalDirectories { get; init; }
    public int EqualFiles { get; init; }
    public int DifferentFiles { get; init; }
    public int MissingLeft { get; init; }
    public int MissingRight { get; init; }
    public int Errors { get; init; }
}

public sealed record DifferenceRecord
{
    public required DifferenceType Type { get; init; }
    public required string Path { get; init; }
    public long? LeftSize { get; init; }
    public long? RightSize { get; init; }
    public string? Algorithm { get; init; }
    public string? Reason { get; init; }
}

public sealed record ComparisonNode
{
    public required string Name { get; init; }
    public required string RelativePath { get; init; }
    public required bool IsDirectory { get; init; }
    public required DifferenceType? Status { get; init; }
    public required ImmutableArray<ComparisonNode> Children { get; init; }
    public int EqualCount { get; init; }
    public int DiffCount { get; init; }
    public int MissingCount { get; init; }
    public int ErrorCount { get; init; }
    public FileMetadata? LeftMetadata { get; init; }
    public FileMetadata? RightMetadata { get; init; }
}

public sealed record FileMetadata
{
    public long Size { get; init; }
    public DateTime LastWriteTimeUtc { get; init; }
    public string? Hash { get; init; }
}
