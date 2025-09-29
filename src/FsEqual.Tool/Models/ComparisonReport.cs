namespace FsEqual.Tool.Models;

public sealed class ComparisonReport
{
    public required IReadOnlyList<PathComparison> Items { get; init; }
    public required ComparisonSummary Summary { get; init; }
    public required IReadOnlyList<ComparisonError> Errors { get; init; }
    public required string LeftRoot { get; init; }
    public string? RightRoot { get; init; }
    public ComparisonMode Mode { get; init; }
    public HashAlgorithmKind Algorithm { get; init; }
}
