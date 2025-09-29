using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.Extensions.FileSystemGlobbing;
using ParallelCompare.Core.Comparison.Hashing;
using ParallelCompare.Core.Options;

namespace ParallelCompare.Core.Comparison;

public sealed class ComparisonEngine
{
    private readonly FileHashCalculator _hashCalculator;

    public ComparisonEngine(FileHashCalculator? hashCalculator = null)
    {
        _hashCalculator = hashCalculator ?? new FileHashCalculator();
    }

    public Task<ComparisonResult> CompareAsync(ComparisonOptions options)
    {
        return Task.Run(() => CompareInternal(options), options.CancellationToken);
    }

    private ComparisonResult CompareInternal(ComparisonOptions options)
    {
        var cancellationToken = options.CancellationToken;
        cancellationToken.ThrowIfCancellationRequested();

        var matcher = BuildMatcher(options);
        var leftDirectory = new DirectoryInfo(options.LeftPath);
        var rightDirectory = new DirectoryInfo(options.RightPath);

        if (!leftDirectory.Exists)
        {
            throw new DirectoryNotFoundException($"Left directory '{options.LeftPath}' was not found.");
        }

        if (!rightDirectory.Exists)
        {
            throw new DirectoryNotFoundException($"Right directory '{options.RightPath}' was not found.");
        }

        var root = CompareDirectory(
            relativePath: string.Empty,
            displayName: leftDirectory.Name,
            leftDirectory,
            rightDirectory,
            matcher,
            options,
            cancellationToken);

        var summary = Summarize(root);
        return new ComparisonResult(root, summary);
    }

    private ComparisonNode CompareDirectory(
        string relativePath,
        string displayName,
        DirectoryInfo left,
        DirectoryInfo right,
        Matcher? matcher,
        ComparisonOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var children = new List<ComparisonNode>();
        var comparer = options.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

        var leftEntries = EnumerateEntries(left, relativePath, matcher, comparer);
        var rightEntries = EnumerateEntries(right, relativePath, matcher, comparer);

        var names = new SortedSet<string>(comparer);
        names.UnionWith(leftEntries.Keys);
        names.UnionWith(rightEntries.Keys);

        foreach (var name in names)
        {
            var childRelativePath = string.IsNullOrEmpty(relativePath)
                ? name
                : Path.Combine(relativePath, name);

            leftEntries.TryGetValue(name, out var leftInfo);
            rightEntries.TryGetValue(name, out var rightInfo);

            var leftDir = leftInfo as DirectoryInfo;
            var rightDir = rightInfo as DirectoryInfo;
            var leftIsDirectory = leftDir is not null;
            var rightIsDirectory = rightDir is not null;

            if (leftDir is not null && rightDir is not null)
            {
                var directoryNode = CompareDirectory(
                    childRelativePath,
                    name,
                    leftDir,
                    rightDir,
                    matcher,
                    options,
                    cancellationToken);
                children.Add(directoryNode);
                continue;
            }

            if (leftDir is not null && rightInfo is FileInfo rightFile)
            {
                children.Add(BuildTypeMismatchNode(
                    name,
                    childRelativePath,
                    leftDir,
                    rightFile,
                    cancellationToken));
                continue;
            }

            if (rightDir is not null && leftInfo is FileInfo leftFile)
            {
                children.Add(BuildTypeMismatchNode(
                    name,
                    childRelativePath,
                    leftFile,
                    rightDir,
                    cancellationToken));
                continue;
            }

            if (leftDir is not null)
            {
                var leftOnlyNode = BuildSingleSideDirectory(
                    name,
                    childRelativePath,
                    leftDir,
                    ComparisonStatus.LeftOnly,
                    matcher,
                    cancellationToken);
                children.Add(leftOnlyNode);
                continue;
            }

            if (rightDir is not null)
            {
                var rightOnlyNode = BuildSingleSideDirectory(
                    name,
                    childRelativePath,
                    rightDir,
                    ComparisonStatus.RightOnly,
                    matcher,
                    cancellationToken);
                children.Add(rightOnlyNode);
                continue;
            }

            var fileNode = CompareFile(
                childRelativePath,
                name,
                leftInfo as FileInfo,
                rightInfo as FileInfo,
                options,
                cancellationToken);
            children.Add(fileNode);
        }

        var status = DetermineDirectoryStatus(children);
        return new ComparisonNode(
            displayName,
            relativePath,
            ComparisonNodeType.Directory,
            status,
            null,
            children.ToImmutableArray());
    }

    private ComparisonNode CompareFile(
        string relativePath,
        string displayName,
        FileInfo? left,
        FileInfo? right,
        ComparisonOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (left is null && right is null)
        {
            throw new InvalidOperationException("Unexpected state: both file references are null.");
        }

        if (left is null)
        {
            return new ComparisonNode(
                displayName,
                relativePath,
                ComparisonNodeType.File,
                ComparisonStatus.RightOnly,
                new FileComparisonDetail(
                    null,
                    right!.Length,
                    null,
                    right.LastWriteTimeUtc,
                    null,
                    null,
                    null),
                ImmutableArray<ComparisonNode>.Empty);
        }

        if (right is null)
        {
            return new ComparisonNode(
                displayName,
                relativePath,
                ComparisonNodeType.File,
                ComparisonStatus.LeftOnly,
                new FileComparisonDetail(
                    left.Length,
                    null,
                    left.LastWriteTimeUtc,
                    null,
                    null,
                    null,
                    null),
                ImmutableArray<ComparisonNode>.Empty);
        }

        try
        {
            var status = CompareFileContents(left, right, options, cancellationToken, out var detail);
            return new ComparisonNode(
                displayName,
                relativePath,
                ComparisonNodeType.File,
                status,
                detail,
                ImmutableArray<ComparisonNode>.Empty);
        }
        catch (Exception ex)
        {
            return new ComparisonNode(
                displayName,
                relativePath,
                ComparisonNodeType.File,
                ComparisonStatus.Error,
                new FileComparisonDetail(
                    left.Length,
                    right.Length,
                    left.LastWriteTimeUtc,
                    right.LastWriteTimeUtc,
                    null,
                    null,
                    ex.Message),
                ImmutableArray<ComparisonNode>.Empty);
        }
    }

    private ComparisonStatus CompareFileContents(
        FileInfo left,
        FileInfo right,
        ComparisonOptions options,
        CancellationToken cancellationToken,
        out FileComparisonDetail detail)
    {
        IReadOnlyDictionary<HashAlgorithmType, string>? leftHashes = null;
        IReadOnlyDictionary<HashAlgorithmType, string>? rightHashes = null;

        if (options.Mode == ComparisonMode.Quick)
        {
            var equal = left.Length == right.Length
                && WithinTolerance(left.LastWriteTimeUtc, right.LastWriteTimeUtc, options.ModifiedTimeTolerance);

            if (!equal && options.HashAlgorithms.IsDefaultOrEmpty)
            {
                detail = new FileComparisonDetail(
                    left.Length,
                    right.Length,
                    left.LastWriteTimeUtc,
                    right.LastWriteTimeUtc,
                    null,
                    null,
                    null);
                return ComparisonStatus.Different;
            }
        }

        if (options.HashAlgorithms.IsDefaultOrEmpty)
        {
            // Quick mode fallback: read bytes when metadata mismatch.
            if (!FilesBinaryEqual(left.FullName, right.FullName, cancellationToken))
            {
                detail = new FileComparisonDetail(
                    left.Length,
                    right.Length,
                    left.LastWriteTimeUtc,
                    right.LastWriteTimeUtc,
                    null,
                    null,
                    null);
                return ComparisonStatus.Different;
            }

            detail = new FileComparisonDetail(
                left.Length,
                right.Length,
                left.LastWriteTimeUtc,
                right.LastWriteTimeUtc,
                null,
                null,
                null);
            return ComparisonStatus.Equal;
        }

        leftHashes = _hashCalculator.ComputeHashes(left.FullName, options.HashAlgorithms, cancellationToken);
        rightHashes = _hashCalculator.ComputeHashes(right.FullName, options.HashAlgorithms, cancellationToken);

        var status = HashesEqual(leftHashes, rightHashes)
            ? ComparisonStatus.Equal
            : ComparisonStatus.Different;

        detail = new FileComparisonDetail(
            left.Length,
            right.Length,
            left.LastWriteTimeUtc,
            right.LastWriteTimeUtc,
            leftHashes,
            rightHashes,
            null);
        return status;
    }

    private static bool FilesBinaryEqual(string leftPath, string rightPath, CancellationToken cancellationToken)
    {
        const int BufferSize = 8192;
        using var left = File.OpenRead(leftPath);
        using var right = File.OpenRead(rightPath);

        if (left.Length != right.Length)
        {
            return false;
        }

        var leftBuffer = new byte[BufferSize];
        var rightBuffer = new byte[BufferSize];

        int leftRead;
        while ((leftRead = left.Read(leftBuffer, 0, BufferSize)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rightRead = right.Read(rightBuffer, 0, BufferSize);
            if (leftRead != rightRead)
            {
                return false;
            }

            for (var i = 0; i < leftRead; i++)
            {
                if (leftBuffer[i] != rightBuffer[i])
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static ComparisonStatus DetermineDirectoryStatus(IEnumerable<ComparisonNode> children)
    {
        var hasError = false;
        var hasDifferent = false;
        var hasLeftOnly = false;
        var hasRightOnly = false;

        foreach (var child in children)
        {
            switch (child.Status)
            {
                case ComparisonStatus.Error:
                    hasError = true;
                    break;
                case ComparisonStatus.Different:
                    hasDifferent = true;
                    break;
                case ComparisonStatus.LeftOnly:
                    hasLeftOnly = true;
                    break;
                case ComparisonStatus.RightOnly:
                    hasRightOnly = true;
                    break;
            }
        }

        if (hasError)
        {
            return ComparisonStatus.Error;
        }

        if (hasDifferent || (hasLeftOnly && hasRightOnly))
        {
            return ComparisonStatus.Different;
        }

        if (hasLeftOnly)
        {
            return ComparisonStatus.LeftOnly;
        }

        if (hasRightOnly)
        {
            return ComparisonStatus.RightOnly;
        }

        return ComparisonStatus.Equal;
    }

    private static ComparisonSummary Summarize(ComparisonNode root)
    {
        var totals = (total: 0, equal: 0, different: 0, leftOnly: 0, rightOnly: 0, error: 0);
        Traverse(root);
        return new ComparisonSummary(totals.total, totals.equal, totals.different, totals.leftOnly, totals.rightOnly, totals.error);

        void Traverse(ComparisonNode node)
        {
            if (node.NodeType == ComparisonNodeType.File)
            {
                totals.total++;
                switch (node.Status)
                {
                    case ComparisonStatus.Equal:
                        totals.equal++;
                        break;
                    case ComparisonStatus.Different:
                        totals.different++;
                        break;
                    case ComparisonStatus.LeftOnly:
                        totals.leftOnly++;
                        break;
                    case ComparisonStatus.RightOnly:
                        totals.rightOnly++;
                        break;
                    case ComparisonStatus.Error:
                        totals.error++;
                        break;
                }
            }

            foreach (var child in node.Children)
            {
                Traverse(child);
            }
        }
    }

    private static bool WithinTolerance(DateTimeOffset left, DateTimeOffset right, TimeSpan? tolerance)
    {
        if (tolerance is null)
        {
            return left == right;
        }

        var delta = (left - right).Duration();
        return delta <= tolerance.Value;
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

    private static Matcher? BuildMatcher(ComparisonOptions options)
    {
        if (options.IgnorePatterns.IsDefaultOrEmpty)
        {
            return null;
        }

        var matcher = new Matcher(options.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
        matcher.AddInclude("**/*");
        foreach (var pattern in options.IgnorePatterns)
        {
            matcher.AddExclude(pattern);
        }

        return matcher;
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

    private ComparisonNode BuildSingleSideDirectory(
        string displayName,
        string relativePath,
        DirectoryInfo directory,
        ComparisonStatus status,
        Matcher? matcher,
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

            var childRelativePath = string.IsNullOrEmpty(relativePath)
                ? entry.Name
                : Path.Combine(relativePath, entry.Name);

            if (entry is DirectoryInfo childDirectory)
            {
                children.Add(BuildSingleSideDirectory(
                    entry.Name,
                    childRelativePath,
                    childDirectory,
                    status,
                    matcher,
                    cancellationToken));
            }
            else if (entry is FileInfo file)
            {
                var detail = status == ComparisonStatus.LeftOnly
                    ? new FileComparisonDetail(file.Length, null, file.LastWriteTimeUtc, null, null, null, null)
                    : new FileComparisonDetail(null, file.Length, null, file.LastWriteTimeUtc, null, null, null);

                children.Add(new ComparisonNode(
                    entry.Name,
                    childRelativePath,
                    ComparisonNodeType.File,
                    status,
                    detail,
                    ImmutableArray<ComparisonNode>.Empty));
            }
        }

        return new ComparisonNode(
            displayName,
            relativePath,
            ComparisonNodeType.Directory,
            status,
            null,
            children.ToImmutableArray());
    }

    private ComparisonNode BuildTypeMismatchNode(
        string displayName,
        string relativePath,
        FileSystemInfo left,
        FileSystemInfo right,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        long? leftSize = left is FileInfo leftFile ? leftFile.Length : null;
        long? rightSize = right is FileInfo rightFile ? rightFile.Length : null;

        var leftModified = left switch
        {
            FileInfo lf => lf.LastWriteTimeUtc,
            DirectoryInfo ld => ld.LastWriteTimeUtc,
            _ => (DateTimeOffset?)null
        };

        var rightModified = right switch
        {
            FileInfo rf => rf.LastWriteTimeUtc,
            DirectoryInfo rd => rd.LastWriteTimeUtc,
            _ => (DateTimeOffset?)null
        };

        var message = left is DirectoryInfo
            ? "Left side is a directory while right side is a file."
            : "Left side is a file while right side is a directory.";

        return new ComparisonNode(
            displayName,
            relativePath,
            ComparisonNodeType.File,
            ComparisonStatus.Different,
            new FileComparisonDetail(
                leftSize,
                rightSize,
                leftModified,
                rightModified,
                null,
                null,
                message),
            ImmutableArray<ComparisonNode>.Empty);
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
}
