namespace FsEqual.Tool.Comparison;

public sealed record ComparisonOptions
{
    public required string Left { get; init; }
    public string? Right { get; init; }
    public int MaxDegreeOfParallelism { get; init; }
    public ComparisonMode Mode { get; init; }
    public HashAlgorithmKind Algorithm { get; init; }
    public IReadOnlyList<string> Ignore { get; init; } = Array.Empty<string>();
    public bool CaseSensitive { get; init; }
    public bool FollowSymlinks { get; init; }
    public TimeSpan? MtimeTolerance { get; init; }
    public VerbosityLevel Verbosity { get; init; }
    public TimeSpan? Timeout { get; init; }
    public FailOnRule FailOn { get; init; }
    public string? Profile { get; init; }
    public string? ConfigPath { get; init; }
    public string? BaselinePath { get; init; }
}

public enum ComparisonMode
{
    Quick,
    Hash
}

public enum HashAlgorithmKind
{
    Crc32,
    Md5,
    Sha256,
    Xxh64
}

public enum VerbosityLevel
{
    Trace,
    Debug,
    Info,
    Warn,
    Error
}

public enum FailOnRule
{
    Any,
    Differences,
    Errors
}
