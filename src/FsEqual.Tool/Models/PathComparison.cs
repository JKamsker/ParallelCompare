namespace FsEqual.Tool.Models;

public sealed record PathComparison(
    string RelativePath,
    PathKind Kind,
    ComparisonStatus Status,
    FileMetadata? Left,
    FileMetadata? Right,
    string? Reason = null);
