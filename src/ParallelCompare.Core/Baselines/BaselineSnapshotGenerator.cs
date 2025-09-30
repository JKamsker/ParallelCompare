using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.Extensions.FileSystemGlobbing;
using ParallelCompare.Core.Comparison;
using ParallelCompare.Core.Comparison.Hashing;
using ParallelCompare.Core.FileSystem;
using ParallelCompare.Core.Options;

namespace ParallelCompare.Core.Baselines;

/// <summary>
/// Creates baseline manifests from live directory trees.
/// </summary>
public sealed class BaselineSnapshotGenerator
{
    private readonly FileHashCalculator _hashCalculator;

    /// <summary>
    /// Initializes a new instance of the <see cref="BaselineSnapshotGenerator"/> class.
    /// </summary>
    /// <param name="hashCalculator">Optional hash calculator to reuse for file hashing.</param>
    public BaselineSnapshotGenerator(FileHashCalculator? hashCalculator = null)
    {
        _hashCalculator = hashCalculator ?? new FileHashCalculator();
    }

    /// <summary>
    /// Creates a baseline manifest using the provided comparison settings.
    /// </summary>
    /// <param name="settings">Resolved settings describing how to capture the snapshot.</param>
    /// <param name="cancellationToken">Token used to cancel the snapshot operation.</param>
    /// <returns>The generated baseline manifest.</returns>
    public BaselineManifest CreateSnapshot(ResolvedCompareSettings settings, CancellationToken cancellationToken)
    {
        var fileSystem = settings.FileSystem ?? PhysicalFileSystem.Instance;
        var directory = fileSystem.GetDirectory(settings.LeftPath);
        if (!directory.Exists)
        {
            throw new DirectoryNotFoundException($"Left directory '{settings.LeftPath}' was not found.");
        }

        var matcher = BuildMatcher(settings.IgnorePatterns, settings.CaseSensitive);
        var root = BuildDirectory(directory, string.Empty, matcher, settings, cancellationToken);

        return new BaselineManifest(
            BaselineManifest.CurrentVersion,
            Path.GetFullPath(settings.LeftPath),
            DateTimeOffset.UtcNow,
            settings.IgnorePatterns,
            settings.CaseSensitive,
            settings.ModifiedTimeTolerance,
            settings.Algorithms,
            root);
    }

    private BaselineEntry BuildDirectory(
        IDirectoryEntry directory,
        string relativePath,
        Matcher? matcher,
        ResolvedCompareSettings settings,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var comparer = settings.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        var children = new List<BaselineEntry>();

        foreach (var entry in directory.EnumerateEntries())
        {
            if (ShouldIgnore(entry, relativePath, matcher))
            {
                continue;
            }

            var childRelative = string.IsNullOrEmpty(relativePath)
                ? entry.Name
                : Path.Combine(relativePath, entry.Name);

            if (entry is IDirectoryEntry childDirectory)
            {
                var child = BuildDirectory(childDirectory, childRelative, matcher, settings, cancellationToken);
                children.Add(child);
            }
            else if (entry is IFileEntry file)
            {
                var child = BuildFileEntry(file, childRelative, settings, cancellationToken);
                children.Add(child);
            }
        }

        var ordered = children
            .OrderBy(child => child.Name, comparer)
            .ToImmutableArray();

        return new BaselineEntry(
            directory.Name,
            relativePath,
            BaselineEntryType.Directory,
            null,
            directory.LastWriteTimeUtc,
            ImmutableDictionary<HashAlgorithmType, string>.Empty,
            ordered);
    }

    private BaselineEntry BuildFileEntry(
        IFileEntry file,
        string relativePath,
        ResolvedCompareSettings settings,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var hashes = settings.Algorithms.IsDefaultOrEmpty
            ? ImmutableDictionary<HashAlgorithmType, string>.Empty
            : _hashCalculator
                .ComputeHashes(file, settings.Algorithms, cancellationToken)
                .ToImmutableDictionary(pair => pair.Key, pair => pair.Value, HashAlgorithmComparer.Instance);

        return new BaselineEntry(
            file.Name,
            relativePath,
            BaselineEntryType.File,
            file.Length,
            file.LastWriteTimeUtc,
            hashes,
            ImmutableArray<BaselineEntry>.Empty);
    }

    private static Matcher? BuildMatcher(ImmutableArray<string> patterns, bool caseSensitive)
    {
        if (patterns.IsDefaultOrEmpty)
        {
            return null;
        }

        var matcher = new Matcher(caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
        matcher.AddInclude("**/*");
        foreach (var pattern in patterns)
        {
            matcher.AddExclude(pattern);
        }

        return matcher;
    }

    private static bool ShouldIgnore(IFileSystemEntry entry, string parentRelativePath, Matcher? matcher)
    {
        if (matcher is null)
        {
            return false;
        }

        var relativePath = string.IsNullOrEmpty(parentRelativePath)
            ? entry.Name
            : Path.Combine(parentRelativePath, entry.Name);

        return matcher.Match(relativePath).HasMatches;
    }

    /// <summary>
    /// Provides ordinal equality for <see cref="HashAlgorithmType"/> comparisons when building snapshots.
    /// </summary>
    private sealed class HashAlgorithmComparer : IEqualityComparer<HashAlgorithmType>
    {
        /// <summary>
        /// Gets the singleton comparer instance.
        /// </summary>
        public static readonly HashAlgorithmComparer Instance = new();

        /// <inheritdoc />
        public bool Equals(HashAlgorithmType x, HashAlgorithmType y) => x == y;

        /// <inheritdoc />
        public int GetHashCode(HashAlgorithmType obj) => (int)obj;
    }
}
