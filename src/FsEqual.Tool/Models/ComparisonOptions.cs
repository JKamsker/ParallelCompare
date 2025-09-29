namespace FsEqual.Tool.Models;

public sealed class ComparisonOptions
{
    public ComparisonMode Mode { get; init; } = ComparisonMode.Quick;

    public HashAlgorithmKind Algorithm { get; init; } = HashAlgorithmKind.Crc32;

    public int Threads { get; init; } = Environment.ProcessorCount;

    public bool CaseSensitive { get; init; }

    public bool FollowSymlinks { get; init; }

    public IReadOnlyList<string> IgnoreGlobs { get; init; } = Array.Empty<string>();

    public double? MtimeToleranceSeconds { get; init; }

    public bool NoProgress { get; init; }
}
