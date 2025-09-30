using System;
using System.Collections.Immutable;
using System.Threading;
using ParallelCompare.Core.FileSystem;
using ParallelCompare.Core.Options;

namespace ParallelCompare.Core.Baselines;

/// <summary>
/// Provides configuration required to compare the current file system against a stored baseline manifest.
/// </summary>
public sealed record BaselineComparisonOptions
{
    /// <summary>
    /// Gets the path that should be evaluated against the baseline snapshot.
    /// </summary>
    public required string LeftPath { get; init; }

    /// <summary>
    /// Gets the path to the baseline manifest file on disk.
    /// </summary>
    public required string BaselinePath { get; init; }

    /// <summary>
    /// Gets the parsed manifest that defines the expected baseline state.
    /// </summary>
    public required BaselineManifest Manifest { get; init; }

    /// <summary>
    /// Gets the hash algorithms that should be calculated for file comparisons.
    /// </summary>
    public ImmutableArray<HashAlgorithmType> HashAlgorithms { get; init; } = ImmutableArray<HashAlgorithmType>.Empty;

    /// <summary>
    /// Gets the ignore patterns used to filter entries when comparing with the baseline.
    /// </summary>
    public ImmutableArray<string> IgnorePatterns { get; init; } = ImmutableArray<string>.Empty;

    /// <summary>
    /// Gets a value indicating whether comparisons should respect case sensitivity.
    /// </summary>
    public bool CaseSensitive { get; init; }

    /// <summary>
    /// Gets the tolerance window for modified timestamps when evaluating equality.
    /// </summary>
    public TimeSpan? ModifiedTimeTolerance { get; init; }

    /// <summary>
    /// Gets the comparison mode that controls how files and directories are evaluated.
    /// </summary>
    public ComparisonMode Mode { get; init; }

    /// <summary>
    /// Gets the token used to observe cancellation requests during the comparison.
    /// </summary>
    public CancellationToken CancellationToken { get; init; }

    /// <summary>
    /// Gets the file system abstraction used to load files and directories.
    /// </summary>
    public IFileSystem FileSystem { get; init; } = PhysicalFileSystem.Instance;
}
