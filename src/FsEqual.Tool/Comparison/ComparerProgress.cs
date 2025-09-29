namespace FsEqual.Tool.Comparison;

public sealed record ComparerProgress
{
    public required string Stage { get; init; }
    public int Completed { get; init; }
    public int Total { get; init; }
    public string? Message { get; init; }
}
