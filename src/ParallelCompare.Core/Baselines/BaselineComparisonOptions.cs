using System;
using System.Collections.Immutable;
using System.Threading;
using ParallelCompare.Core.FileSystem;
using ParallelCompare.Core.Options;

namespace ParallelCompare.Core.Baselines;

public sealed record BaselineComparisonOptions
{
    public required string LeftPath { get; init; }
    public required string BaselinePath { get; init; }
    public required BaselineManifest Manifest { get; init; }
    public ImmutableArray<HashAlgorithmType> HashAlgorithms { get; init; } = ImmutableArray<HashAlgorithmType>.Empty;
    public ImmutableArray<string> IgnorePatterns { get; init; } = ImmutableArray<string>.Empty;
    public bool CaseSensitive { get; init; }
    public TimeSpan? ModifiedTimeTolerance { get; init; }
    public ComparisonMode Mode { get; init; }
    public CancellationToken CancellationToken { get; init; }
    public IFileSystem FileSystem { get; init; } = PhysicalFileSystem.Instance;
}
