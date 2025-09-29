using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.FileSystemGlobbing;
using ParallelCompare.Core.Comparison.Hashing;
using ParallelCompare.Core.FileSystem;
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
        var fileSystem = options.FileSystem ?? PhysicalFileSystem.Instance;
        var leftDirectory = fileSystem.GetDirectory(options.LeftPath);
        var rightDirectory = fileSystem.GetDirectory(options.RightPath);

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

        var summary = ComparisonSummaryCalculator.Calculate(root);
        return new ComparisonResult(
            Path.GetFullPath(options.LeftPath),
            Path.GetFullPath(options.RightPath),
            root,
            summary);
    }

    private ComparisonNode CompareDirectory(
        string relativePath,
        string displayName,
        IDirectoryEntry left,
        IDirectoryEntry right,
        Matcher? matcher,
        ComparisonOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var comparer = options.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        var updateSink = options.UpdateSink;

        updateSink?.DirectoryDiscovered(relativePath, displayName);

        var leftEntries = EnumerateEntries(left, relativePath, matcher, comparer);
        var rightEntries = EnumerateEntries(right, relativePath, matcher, comparer);

        var names = new SortedSet<string>(comparer);
        names.UnionWith(leftEntries.Keys);
        names.UnionWith(rightEntries.Keys);

        var workItems = names
            .Select(name =>
            {
                var childRelativePath = string.IsNullOrEmpty(relativePath)
                    ? name
                    : Path.Combine(relativePath, name);

                leftEntries.TryGetValue(name, out var leftInfo);
                rightEntries.TryGetValue(name, out var rightInfo);

                return new EntryWorkItem(name, childRelativePath, leftInfo, rightInfo);
            })
            .ToArray();

        var parallelOptions = CreateParallelOptions(options, cancellationToken);
        Parallel.ForEach(workItems, parallelOptions, item =>
        {
            item.Node = BuildNode(
                item,
                matcher,
                options,
                cancellationToken);

            if (updateSink is not null && item.Node is not null)
            {
                updateSink.NodeCompleted(item.Node);
            }
        });

        var orderedChildren = workItems
            .OrderBy(item => item.Name, comparer)
            .Select(item => item.Node ?? throw new InvalidOperationException("Comparison node was not produced for entry."))
            .ToImmutableArray();

        var status = DetermineDirectoryStatus(orderedChildren);
        var directoryNode = new ComparisonNode(
            displayName,
            relativePath,
            ComparisonNodeType.Directory,
            status,
            null,
            orderedChildren);

        if (string.IsNullOrEmpty(relativePath))
        {
            updateSink?.NodeCompleted(directoryNode);
        }

        return directoryNode;
    }

    private ComparisonNode BuildNode(
        EntryWorkItem item,
        Matcher? matcher,
        ComparisonOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var leftDir = item.Left as IDirectoryEntry;
        var rightDir = item.Right as IDirectoryEntry;

        if (leftDir is not null && rightDir is not null)
        {
            return CompareDirectory(
                item.RelativePath,
                item.Name,
                leftDir,
                rightDir,
                matcher,
                options,
                cancellationToken);
        }

        if (leftDir is not null && item.Right is IFileEntry rightFile)
        {
            return BuildTypeMismatchNode(
                item.Name,
                item.RelativePath,
                leftDir,
                rightFile,
                cancellationToken);
        }

        if (rightDir is not null && item.Left is IFileEntry leftFile)
        {
            return BuildTypeMismatchNode(
                item.Name,
                item.RelativePath,
                leftFile,
                rightDir,
                cancellationToken);
        }

        if (leftDir is not null)
        {
            return BuildSingleSideDirectory(
                item.Name,
                item.RelativePath,
                leftDir,
                ComparisonStatus.LeftOnly,
                matcher,
                cancellationToken,
                options.UpdateSink);
        }

        if (rightDir is not null)
        {
            return BuildSingleSideDirectory(
                item.Name,
                item.RelativePath,
                rightDir,
                ComparisonStatus.RightOnly,
                matcher,
                cancellationToken,
                options.UpdateSink);
        }

        return CompareFile(
            item.RelativePath,
            item.Name,
            item.Left as IFileEntry,
            item.Right as IFileEntry,
            options,
            cancellationToken);
    }

    private static ParallelOptions CreateParallelOptions(ComparisonOptions options, CancellationToken cancellationToken)
    {
        var maxDegree = options.MaxDegreeOfParallelism.HasValue && options.MaxDegreeOfParallelism.Value > 0
            ? options.MaxDegreeOfParallelism.Value
            : Environment.ProcessorCount;

        return new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, maxDegree),
            CancellationToken = cancellationToken
        };
    }

    private ComparisonNode CompareFile(
        string relativePath,
        string displayName,
        IFileEntry? left,
        IFileEntry? right,
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
        IFileEntry left,
        IFileEntry right,
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
            if (!FilesBinaryEqual(left, right, cancellationToken))
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

        leftHashes = _hashCalculator.ComputeHashes(left, options.HashAlgorithms, cancellationToken);
        rightHashes = _hashCalculator.ComputeHashes(right, options.HashAlgorithms, cancellationToken);

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

    private static bool FilesBinaryEqual(IFileEntry left, IFileEntry right, CancellationToken cancellationToken)
    {
        const int BufferSize = 8192;
        using var leftStream = left.OpenRead();
        using var rightStream = right.OpenRead();

        if (leftStream.Length != rightStream.Length)
        {
            return false;
        }

        var leftBuffer = new byte[BufferSize];
        var rightBuffer = new byte[BufferSize];

        int leftRead;
        while ((leftRead = leftStream.Read(leftBuffer, 0, BufferSize)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rightRead = rightStream.Read(rightBuffer, 0, BufferSize);
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

    private static bool WithinTolerance(DateTimeOffset left, DateTimeOffset right, TimeSpan? tolerance)
    {
        if (tolerance is null)
        {
            return left == right;
        }

        var delta = (left - right).Duration();
        return delta <= tolerance.Value;
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

    private ComparisonNode BuildSingleSideDirectory(
        string displayName,
        string relativePath,
        IDirectoryEntry directory,
        ComparisonStatus status,
        Matcher? matcher,
        CancellationToken cancellationToken,
        IComparisonUpdateSink? updateSink)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var children = new List<ComparisonNode>();
        foreach (var entry in directory.EnumerateEntries())
        {
            if (ShouldIgnore(entry, relativePath, matcher))
            {
                continue;
            }

            var childRelativePath = string.IsNullOrEmpty(relativePath)
                ? entry.Name
                : Path.Combine(relativePath, entry.Name);

            if (entry is IDirectoryEntry childDirectory)
            {
                var childNode = BuildSingleSideDirectory(
                    entry.Name,
                    childRelativePath,
                    childDirectory,
                    status,
                    matcher,
                    cancellationToken,
                    updateSink);
                children.Add(childNode);
                updateSink?.NodeCompleted(childNode);
            }
            else if (entry is IFileEntry file)
            {
                var detail = status == ComparisonStatus.LeftOnly
                    ? new FileComparisonDetail(file.Length, null, file.LastWriteTimeUtc, null, null, null, null)
                    : new FileComparisonDetail(null, file.Length, null, file.LastWriteTimeUtc, null, null, null);

                var fileNode = new ComparisonNode(
                    entry.Name,
                    childRelativePath,
                    ComparisonNodeType.File,
                    status,
                    detail,
                    ImmutableArray<ComparisonNode>.Empty);
                children.Add(fileNode);
                updateSink?.NodeCompleted(fileNode);
            }
        }

        var directoryNode = new ComparisonNode(
            displayName,
            relativePath,
            ComparisonNodeType.Directory,
            status,
            null,
            children.ToImmutableArray());

        return directoryNode;
    }

    private ComparisonNode BuildTypeMismatchNode(
        string displayName,
        string relativePath,
        IFileSystemEntry left,
        IFileSystemEntry right,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        long? leftSize = left is IFileEntry leftFile ? leftFile.Length : null;
        long? rightSize = right is IFileEntry rightFile ? rightFile.Length : null;

        var leftModified = left.LastWriteTimeUtc;
        var rightModified = right.LastWriteTimeUtc;

        var message = left.EntryType == FileSystemEntryType.Directory
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

    private sealed class EntryWorkItem
    {
        public EntryWorkItem(string name, string relativePath, IFileSystemEntry? left, IFileSystemEntry? right)
        {
            Name = name;
            RelativePath = relativePath;
            Left = left;
            Right = right;
        }

        public string Name { get; }

        public string RelativePath { get; }

        public IFileSystemEntry? Left { get; }

        public IFileSystemEntry? Right { get; }

        public ComparisonNode? Node { get; set; }
    }
}
