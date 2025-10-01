using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.FileSystemGlobbing;
using FsEqual.Core.Comparison;
using FsEqual.Core.Comparison.Hashing;
using FsEqual.Core.FileSystem;
using FsEqual.Core.Options;

namespace FsEqual.Core.Baselines;

/// <summary>
/// Executes comparisons between a live directory tree and a previously recorded baseline manifest.
/// </summary>
public sealed class BaselineComparisonEngine
{
    private readonly FileHashCalculator _hashCalculator;

    /// <summary>
    /// Initializes a new instance of the <see cref="BaselineComparisonEngine"/> class.
    /// </summary>
    /// <param name="hashCalculator">Optional hash calculator to reuse for file hashing operations.</param>
    public BaselineComparisonEngine(FileHashCalculator? hashCalculator = null)
    {
        _hashCalculator = hashCalculator ?? new FileHashCalculator();
    }

    /// <summary>
    /// Executes the comparison asynchronously using the provided options.
    /// </summary>
    /// <param name="options">Comparison configuration describing the baseline and input directory.</param>
    /// <returns>The resulting comparison tree.</returns>
    public Task<ComparisonResult> CompareAsync(BaselineComparisonOptions options)
    {
        return Task.Run(() => CompareInternal(options), options.CancellationToken);
    }

    private ComparisonResult CompareInternal(BaselineComparisonOptions options)
    {
        var cancellationToken = options.CancellationToken;
        cancellationToken.ThrowIfCancellationRequested();

        var fileSystem = options.FileSystem ?? PhysicalFileSystem.Instance;
        var directory = fileSystem.GetDirectory(options.LeftPath);
        if (!directory.Exists)
        {
            throw new DirectoryNotFoundException($"Left directory '{options.LeftPath}' was not found.");
        }

        var manifest = options.Manifest;
        var matcher = BuildMatcher(manifest.IgnorePatterns, manifest.CaseSensitive);
        var root = CompareDirectory(
            string.Empty,
            directory.Name,
            directory,
            manifest.Root,
            matcher,
            options,
            cancellationToken,
            options.ProgressSink);

        var summary = ComparisonSummaryCalculator.Calculate(root);
        var metadata = new BaselineMetadata(
            options.BaselinePath,
            manifest.SourcePath,
            manifest.CreatedAt,
            manifest.Algorithms);

        return new ComparisonResult(
            Path.GetFullPath(options.LeftPath),
            manifest.SourcePath,
            root,
            summary,
            metadata);
    }

    private ComparisonNode CompareDirectory(
        string relativePath,
        string displayName,
        IDirectoryEntry directory,
        BaselineEntry baseline,
        Matcher? matcher,
        BaselineComparisonOptions options,
        CancellationToken cancellationToken,
        IComparisonProgressSink? progressSink)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var comparer = options.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        var leftEntries = EnumerateEntries(directory, relativePath, matcher, comparer);
        var baselineEntries = baseline.Children.ToDictionary(entry => entry.Name, comparer);
        var names = new SortedSet<string>(comparer);
        names.UnionWith(leftEntries.Keys);
        names.UnionWith(baselineEntries.Keys);

        var children = new List<ComparisonNode>();
        foreach (var name in names)
        {
            cancellationToken.ThrowIfCancellationRequested();

            leftEntries.TryGetValue(name, out var leftInfo);
            baselineEntries.TryGetValue(name, out var baselineEntry);
            var childRelativePath = string.IsNullOrEmpty(relativePath)
                ? name
                : Path.Combine(relativePath, name);

            var discoveryReported = false;
            if (progressSink is not null)
            {
                var leftFile = leftInfo as IFileEntry;
                long? baselineSize = baselineEntry?.IsFile == true ? baselineEntry.Size : null;
                if (leftFile is not null || baselineSize is not null)
                {
                    progressSink.FileDiscovered(childRelativePath, leftFile?.Length, baselineSize);
                    discoveryReported = true;
                }
            }

            ComparisonNode? child = null;
            if (leftInfo is IDirectoryEntry leftDirectory)
            {
                if (baselineEntry?.IsDirectory == true)
                {
                    child = CompareDirectory(
                        childRelativePath,
                        name,
                        leftDirectory,
                        baselineEntry,
                        matcher,
                        options,
                        cancellationToken,
                        progressSink);
                }
                else if (baselineEntry is null)
                {
                    child = BuildLeftOnlyDirectory(
                        leftDirectory,
                        childRelativePath,
                        name,
                        matcher,
                        options,
                        cancellationToken,
                        progressSink);
                }
                else
                {
                    child = BuildTypeMismatchNode(name, childRelativePath, leftDirectory, baselineEntry);
                }
            }
            else if (leftInfo is IFileEntry leftFile)
            {
                if (baselineEntry?.IsFile == true)
                {
                    child = CompareFile(
                        leftFile,
                        baselineEntry,
                        childRelativePath,
                        name,
                        options,
                        cancellationToken,
                        progressSink);
                }
                else if (baselineEntry is null)
                {
                    child = BuildLeftOnlyFile(leftFile, childRelativePath, name);
                }
                else
                {
                    child = BuildTypeMismatchNode(name, childRelativePath, leftFile, baselineEntry);
                }
            }
            else if (baselineEntry is not null)
            {
                if (baselineEntry.IsDirectory)
                {
                    child = BuildBaselineOnlyDirectory(baselineEntry, childRelativePath, progressSink);
                }
                else
                {
                    child = BuildBaselineOnlyFileWithProgress(baselineEntry, childRelativePath, progressSink, discoveryReported);
                }
            }

            if (child is not null)
            {
                children.Add(child);

                if (child.NodeType == ComparisonNodeType.File)
                {
                    progressSink?.FileCompleted(child.RelativePath, child.Status);
                }
            }
        }

        var orderedChildren = children
            .OrderBy(child => child.Name, comparer)
            .ToImmutableArray();

        var status = DetermineDirectoryStatus(orderedChildren);
        return new ComparisonNode(
            displayName,
            relativePath,
            ComparisonNodeType.Directory,
            status,
            null,
            orderedChildren);
    }

    private ComparisonNode CompareFile(
        IFileEntry left,
        BaselineEntry baseline,
        string relativePath,
        string displayName,
        BaselineComparisonOptions options,
        CancellationToken cancellationToken,
        IComparisonProgressSink? progressSink)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var algorithms = DetermineAlgorithms(options, baseline);
        IReadOnlyDictionary<HashAlgorithmType, string>? leftHashes = null;
        IReadOnlyDictionary<HashAlgorithmType, string>? baselineHashes = null;

        if (!algorithms.IsDefaultOrEmpty)
        {
            leftHashes = _hashCalculator.ComputeHashes(
                left,
                algorithms,
                cancellationToken,
                progressSink,
                ComparisonSide.Left);
            baselineHashes = algorithms.ToDictionary(
                algorithm => algorithm,
                algorithm => baseline.Hashes.TryGetValue(algorithm, out var value)
                    ? value
                    : throw new InvalidOperationException($"Baseline manifest is missing hash '{algorithm}' for '{relativePath}'."));
        }

        ComparisonStatus status;
        if (leftHashes is not null && baselineHashes is not null)
        {
            status = HashesEqual(leftHashes, baselineHashes)
                ? ComparisonStatus.Equal
                : ComparisonStatus.Different;
        }
        else
        {
            var equal = baseline.Size.HasValue
                && left.Length == baseline.Size.Value
                && WithinTolerance(left.LastWriteTimeUtc, baseline.Modified, options.ModifiedTimeTolerance);
            status = equal ? ComparisonStatus.Equal : ComparisonStatus.Different;
        }

        var detail = new FileComparisonDetail(
            left.Length,
            baseline.Size,
            left.LastWriteTimeUtc,
            baseline.Modified,
            leftHashes,
            baselineHashes,
            null);

        return new ComparisonNode(
            displayName,
            relativePath,
            ComparisonNodeType.File,
            status,
            detail,
            ImmutableArray<ComparisonNode>.Empty);
    }

    private static ComparisonStatus DetermineDirectoryStatus(ImmutableArray<ComparisonNode> children)
    {
        if (children.Length == 0)
        {
            return ComparisonStatus.Equal;
        }

        var hasDifferences = children.Any(child => child.Status is ComparisonStatus.Different or ComparisonStatus.LeftOnly or ComparisonStatus.RightOnly or ComparisonStatus.Error);
        if (!hasDifferences)
        {
            return ComparisonStatus.Equal;
        }

        if (children.All(child => child.Status == ComparisonStatus.LeftOnly))
        {
            return ComparisonStatus.LeftOnly;
        }

        if (children.All(child => child.Status == ComparisonStatus.RightOnly))
        {
            return ComparisonStatus.RightOnly;
        }

        return ComparisonStatus.Different;
    }

    private ComparisonNode BuildLeftOnlyDirectory(
        IDirectoryEntry directory,
        string relativePath,
        string displayName,
        Matcher? matcher,
        BaselineComparisonOptions options,
        CancellationToken cancellationToken,
        IComparisonProgressSink? progressSink)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var children = new List<ComparisonNode>();
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
                var child = BuildLeftOnlyDirectory(
                    childDirectory,
                    childRelative,
                    entry.Name,
                    matcher,
                    options,
                    cancellationToken,
                    progressSink);
                children.Add(child);
            }
            else if (entry is IFileEntry file)
            {
                progressSink?.FileDiscovered(childRelative, file.Length, null);
                var node = BuildLeftOnlyFile(file, childRelative, entry.Name);
                children.Add(node);
                progressSink?.FileCompleted(childRelative, node.Status);
            }
        }

        return new ComparisonNode(
            displayName,
            relativePath,
            ComparisonNodeType.Directory,
            ComparisonStatus.LeftOnly,
            null,
            children.ToImmutableArray());
    }

    private ComparisonNode BuildBaselineOnlyDirectory(BaselineEntry entry, string relativePath, IComparisonProgressSink? progressSink)
    {
        var children = entry.Children.Select(child =>
        {
            var childRelative = string.IsNullOrEmpty(relativePath)
                ? child.Name
                : Path.Combine(relativePath, child.Name);

            return child.IsDirectory
                ? BuildBaselineOnlyDirectory(child, childRelative, progressSink)
                : BuildBaselineOnlyFileWithProgress(child, childRelative, progressSink);
        }).ToImmutableArray();

        return new ComparisonNode(
            entry.Name,
            relativePath,
            ComparisonNodeType.Directory,
            ComparisonStatus.RightOnly,
            null,
            children);
    }

    private ComparisonNode BuildBaselineOnlyFileWithProgress(
        BaselineEntry entry,
        string relativePath,
        IComparisonProgressSink? progressSink,
        bool discoveryReported = false)
    {
        if (!discoveryReported)
        {
            progressSink?.FileDiscovered(relativePath, null, entry.Size);
        }

        var node = BuildBaselineOnlyFile(entry, relativePath);
        progressSink?.FileCompleted(relativePath, node.Status);
        return node;
    }

    private static ComparisonNode BuildLeftOnlyFile(IFileEntry file, string relativePath, string displayName)
    {
        var detail = new FileComparisonDetail(
            file.Length,
            null,
            file.LastWriteTimeUtc,
            null,
            null,
            null,
            null);

        return new ComparisonNode(
            displayName,
            relativePath,
            ComparisonNodeType.File,
            ComparisonStatus.LeftOnly,
            detail,
            ImmutableArray<ComparisonNode>.Empty);
    }

    private static ComparisonNode BuildBaselineOnlyFile(BaselineEntry entry, string relativePath)
    {
        var detail = new FileComparisonDetail(
            null,
            entry.Size,
            null,
            entry.Modified,
            null,
            entry.Hashes,
            null);

        return new ComparisonNode(
            entry.Name,
            relativePath,
            ComparisonNodeType.File,
            ComparisonStatus.RightOnly,
            detail,
            ImmutableArray<ComparisonNode>.Empty);
    }

    private static ComparisonNode BuildTypeMismatchNode(string displayName, string relativePath, IFileSystemEntry left, BaselineEntry baseline)
    {
        long? leftSize = left is IFileEntry leftFile ? leftFile.Length : null;
        long? rightSize = baseline.Size;

        DateTimeOffset? leftModified = left.EntryType switch
        {
            FileSystemEntryType.File => left.LastWriteTimeUtc,
            FileSystemEntryType.Directory => left.LastWriteTimeUtc,
            _ => null
        };

        var message = left.EntryType == FileSystemEntryType.Directory
            ? "Left side is a directory while baseline recorded a file."
            : "Left side is a file while baseline recorded a directory.";

        return new ComparisonNode(
            displayName,
            relativePath,
            ComparisonNodeType.File,
            ComparisonStatus.Different,
            new FileComparisonDetail(
                leftSize,
                rightSize,
                leftModified,
                baseline.Modified,
                null,
                baseline.Hashes.Count == 0 ? null : baseline.Hashes,
                message),
            ImmutableArray<ComparisonNode>.Empty);
    }

    private static ImmutableArray<HashAlgorithmType> DetermineAlgorithms(BaselineComparisonOptions options, BaselineEntry entry)
    {
        if (!options.HashAlgorithms.IsDefaultOrEmpty)
        {
            EnsureAlgorithmsAvailable(entry, options.HashAlgorithms);
            return options.HashAlgorithms;
        }

        if (entry.Hashes.Count > 0)
        {
            return entry.Hashes.Keys.ToImmutableArray();
        }

        if (!options.Manifest.Algorithms.IsDefaultOrEmpty)
        {
            EnsureAlgorithmsAvailable(entry, options.Manifest.Algorithms);
            return options.Manifest.Algorithms;
        }

        return ImmutableArray<HashAlgorithmType>.Empty;
    }

    private static void EnsureAlgorithmsAvailable(BaselineEntry entry, ImmutableArray<HashAlgorithmType> algorithms)
    {
        foreach (var algorithm in algorithms)
        {
            if (!entry.Hashes.ContainsKey(algorithm))
            {
                throw new InvalidOperationException($"Baseline manifest does not contain hashes for algorithm '{algorithm}'.");
            }
        }
    }

    private static bool HashesEqual(
        IReadOnlyDictionary<HashAlgorithmType, string> left,
        IReadOnlyDictionary<HashAlgorithmType, string> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        foreach (var (key, leftValue) in left)
        {
            if (!right.TryGetValue(key, out var rightValue))
            {
                return false;
            }

            if (!string.Equals(leftValue, rightValue, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static bool WithinTolerance(DateTimeOffset? left, DateTimeOffset? right, TimeSpan? tolerance)
    {
        if (left is null || right is null)
        {
            return false;
        }

        if (tolerance is null)
        {
            return left.Value == right.Value;
        }

        var delta = (left.Value - right.Value).Duration();
        return delta <= tolerance.Value;
    }

    private static Dictionary<string, IFileSystemEntry> EnumerateEntries(
        IDirectoryEntry directory,
        string relativePath,
        Matcher? matcher,
        StringComparer comparer)
    {
        var result = new Dictionary<string, IFileSystemEntry>(comparer);
        if (!directory.Exists)
        {
            return result;
        }

        foreach (var entry in directory.EnumerateEntries())
        {
            if (ShouldIgnore(entry, relativePath, matcher))
            {
                continue;
            }

            result[entry.Name] = entry;
        }

        return result;
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
}
