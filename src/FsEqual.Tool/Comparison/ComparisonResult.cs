using System.Text.Json.Serialization;

namespace FsEqual.Tool.Comparison;

public sealed record ComparisonResult
{
    public required ComparisonSummary Summary { get; init; }
    public required IReadOnlyList<ComparisonEntry> Differences { get; init; }
    public required IReadOnlyList<ComparisonEntry> DirectoryDifferences { get; init; }
    public required IReadOnlyList<ComparisonEntry> BaselineDifferences { get; init; }
    public required IReadOnlyList<ComparisonIssue> Issues { get; init; }
    public required TimeSpan Duration { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public required DateTimeOffset CompletedAt { get; init; }

    [JsonIgnore]
    public bool AreEqual => Summary.FilesEqual == Summary.FilesCompared &&
                            Summary.FileDifferences == 0 &&
                            Summary.MissingLeft == 0 &&
                            Summary.MissingRight == 0 &&
                            Summary.DirectoryDifferences == 0 &&
                            Summary.Errors == 0 &&
                            Summary.BaselineDifferences == 0;
}

public sealed record ComparisonSummary
{
    public int FilesCompared { get; init; }
    public int FilesEqual { get; init; }
    public int FileDifferences { get; init; }
    public int MissingLeft { get; init; }
    public int MissingRight { get; init; }
    public int DirectoryDifferences { get; init; }
    public int Errors { get; init; }
    public int BaselineDifferences { get; init; }
}

public enum EntryKind
{
    File,
    Directory,
    Baseline
}

public enum EntryStatus
{
    Equal,
    MissingLeft,
    MissingRight,
    TypeMismatch,
    SizeMismatch,
    HashMismatch,
    TimeMismatch,
    MetadataMismatch,
    Error
}

public sealed record ComparisonEntry
{
    public required EntryKind Kind { get; init; }
    public required EntryStatus Status { get; init; }
    public required string Path { get; init; }
    public FileMetadata? Left { get; init; }
    public FileMetadata? Right { get; init; }
    public string? Detail { get; init; }
}

public sealed record FileMetadata
{
    public required long Size { get; init; }
    public required DateTime LastWriteTimeUtc { get; init; }
    public string? Hash { get; init; }
    public HashAlgorithmKind? HashAlgorithm { get; init; }
    public string? Source { get; init; }
}

public sealed record ComparisonIssue
{
    public ComparisonIssue(string message, Exception? exception = null)
    {
        Message = message;
        Exception = exception;
    }

    public string Message { get; }
    [JsonIgnore]
    public Exception? Exception { get; }
}
