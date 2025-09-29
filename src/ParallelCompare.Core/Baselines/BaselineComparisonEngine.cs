using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.FileSystemGlobbing;
using ParallelCompare.Core.Comparison;
using ParallelCompare.Core.Comparison.Hashing;
using ParallelCompare.Core.Options;

namespace ParallelCompare.Core.Baselines;

public sealed class BaselineComparisonEngine
{
    private readonly FileHashCalculator _hashCalculator;

    public BaselineComparisonEngine(FileHashCalculator? hashCalculator = null)
    {
        _hashCalculator = hashCalculator ?? new FileHashCalculator();
    }

    public Task<ComparisonResult> CompareAsync(BaselineComparisonOptions options)
    {
        return Task.Run(() => CompareInternal(options), options.CancellationToken);
    }

    private ComparisonResult CompareInternal(BaselineComparisonOptions options)
    {
        var cancellationToken = options.CancellationToken;
        cancellationToken.ThrowIfCancellationRequested();

        var directory = new DirectoryInfo(options.LeftPath);
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
            cancellationToken);

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
        DirectoryInfo directory,
        BaselineEntry baseline,
        Matcher? matcher,
        BaselineComparisonOptions options,
        CancellationToken cancellationToken)
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

            if (leftInfo is DirectoryInfo leftDirectory)
            {
                if (baselineEntry?.IsDirectory == true)
                {
                    children.Add(CompareDirectory(
                        childRelativePath,
                        name,
                        leftDirectory,
                        baselineEntry,
                        matcher,
                        options,
                        cancellationToken));
                }
                else if (baselineEntry is null)
                {
                    children.Add(BuildLeftOnlyDirectory(leftDirectory, childRelativePath, name, matcher, options, cancellationToken));
                }
                else
                {
                    children.Add(BuildTypeMismatchNode(name, childRelativePath, leftDirectory, baselineEntry));
                }
            }
            else if (leftInfo is FileInfo leftFile)
            {
                if (baselineEntry?.IsFile == true)
                {
                    children.Add(CompareFile(leftFile, baselineEntry, childRelativePath, name, options, cancellationToken));
                }
                else if (baselineEntry is null)
                {
                    children.Add(BuildLeftOnlyFile(leftFile, childRelativePath, name));
                }
                else
                {
                    children.Add(BuildTypeMismatchNode(name, childRelativePath, leftFile, baselineEntry));
                }
            }
            else if (baselineEntry is not null)
            {
                if (baselineEntry.IsDirectory)
                {
                    children.Add(BuildBaselineOnlyDirectory(baselineEntry, childRelativePath));
                }
                else
                {
                    children.Add(BuildBaselineOnlyFile(baselineEntry, childRelativePath));
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
        FileInfo left,
        BaselineEntry baseline,
        string relativePath,
        string displayName,
        BaselineComparisonOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var algorithms = DetermineAlgorithms(options, baseline);
        IReadOnlyDictionary<HashAlgorithmType, string>? leftHashes = null;
        IReadOnlyDictionary<HashAlgorithmType, string>? baselineHashes = null;

        if (!algorithms.IsDefaultOrEmpty)
        {
            leftHashes = _hashCalculator.ComputeHashes(left.FullName, algorithms, cancellationToken);
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
        DirectoryInfo directory,
        string relativePath,
        string displayName,
        Matcher? matcher,
        BaselineComparisonOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var children = new List<ComparisonNode>();
        foreach (var entry in directory.EnumerateFileSystemInfos())
        {
            if (ShouldIgnore(entry, relativePath, matcher))
            {
                continue;
            }

            var childRelative = string.IsNullOrEmpty(relativePath)
                ? entry.Name
                : Path.Combine(relativePath, entry.Name);

            if (entry is DirectoryInfo childDirectory)
            {
                var child = BuildLeftOnlyDirectory(childDirectory, childRelative, entry.Name, matcher, options, cancellationToken);
                children.Add(child);
            }
            else if (entry is FileInfo file)
            {
                children.Add(BuildLeftOnlyFile(file, childRelative, entry.Name));
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

    private ComparisonNode BuildBaselineOnlyDirectory(BaselineEntry entry, string relativePath)
    {
        var children = entry.Children.Select(child =>
        {
            var childRelative = string.IsNullOrEmpty(relativePath)
                ? child.Name
                : Path.Combine(relativePath, child.Name);

            return child.IsDirectory
                ? BuildBaselineOnlyDirectory(child, childRelative)
                : BuildBaselineOnlyFile(child, childRelative);
        }).ToImmutableArray();

        return new ComparisonNode(
            entry.Name,
            relativePath,
            ComparisonNodeType.Directory,
            ComparisonStatus.RightOnly,
            null,
            children);
    }

    private static ComparisonNode BuildLeftOnlyFile(FileInfo file, string relativePath, string displayName)
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

    private static ComparisonNode BuildTypeMismatchNode(string displayName, string relativePath, FileSystemInfo left, BaselineEntry baseline)
    {
        long? leftSize = left switch
        {
            FileInfo lf => lf.Length,
            DirectoryInfo _ => null,
            _ => null
        };

        long? rightSize = baseline.Size;

        DateTimeOffset? leftModified = left switch
        {
            FileInfo lf => lf.LastWriteTimeUtc,
            DirectoryInfo ld => ld.LastWriteTimeUtc,
            _ => null
        };

        var message = left is DirectoryInfo
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

    private static Dictionary<string, FileSystemInfo> EnumerateEntries(
        DirectoryInfo directory,
        string relativePath,
        Matcher? matcher,
        StringComparer comparer)
    {
        var result = new Dictionary<string, FileSystemInfo>(comparer);
        if (!directory.Exists)
        {
            return result;
        }

        foreach (var entry in directory.EnumerateFileSystemInfos())
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

    private static bool ShouldIgnore(FileSystemInfo entry, string parentRelativePath, Matcher? matcher)
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
