using System.Collections.Immutable;
using ParallelCompare.Core.Options;

namespace ParallelCompare.Core.Baselines;

public sealed record BaselineManifest(
    string Version,
    string SourcePath,
    DateTimeOffset CreatedAt,
    ImmutableArray<string> IgnorePatterns,
    bool CaseSensitive,
    TimeSpan? ModifiedTimeTolerance,
    ImmutableArray<HashAlgorithmType> Algorithms,
    BaselineEntry Root
)
{
    public const string CurrentVersion = "1.0";
}
