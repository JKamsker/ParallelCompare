using System.Collections.Concurrent;
using System.Diagnostics;

namespace FsEqual.Tool.Comparison;

internal sealed class DirectoryComparer
{
    public async Task<ComparisonResult> CompareAsync(ComparisonOptions options, IProgress<ComparerProgress>? progress, CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        var ignore = new IgnoreMatcher(options.Ignore, options.CaseSensitive);
        progress?.Report(new ComparerProgress { Stage = "Enumerating", Message = options.Left, Completed = 0, Total = 0 });
        var leftSnapshot = DirectorySnapshot.Load(options.Left, options, ignore, cancellationToken);

        DirectorySnapshot? rightSnapshot = null;
        if (!string.IsNullOrWhiteSpace(options.Right))
        {
            progress?.Report(new ComparerProgress { Stage = "Enumerating", Message = options.Right, Completed = 0, Total = 0 });
            rightSnapshot = DirectorySnapshot.Load(options.Right!, options, new IgnoreMatcher(options.Ignore, options.CaseSensitive), cancellationToken);
        }

        BaselineSnapshot? baseline = null;
        if (!string.IsNullOrWhiteSpace(options.BaselinePath))
        {
            progress?.Report(new ComparerProgress { Stage = "Loading baseline", Message = options.BaselinePath, Completed = 0, Total = 0 });
            baseline = BaselineSnapshot.Load(options.BaselinePath!);
        }

        var issues = new List<ComparisonIssue>();
        issues.AddRange(leftSnapshot.Issues);
        if (rightSnapshot is not null)
        {
            issues.AddRange(rightSnapshot.Issues);
        }

        var differences = new ConcurrentBag<ComparisonEntry>();
        var directoryDifferences = new ConcurrentBag<ComparisonEntry>();
        var baselineDifferences = new ConcurrentBag<ComparisonEntry>();

        var comparer = options.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        var counters = new ComparisonCounters();

        if (rightSnapshot is not null)
        {
            CompareDirectories(leftSnapshot, rightSnapshot, directoryDifferences, counters, comparer);
            await CompareFilesAsync(options, leftSnapshot, rightSnapshot, differences, counters, progress, cancellationToken).ConfigureAwait(false);
        }

        if (baseline is not null)
        {
            CompareWithBaseline(options, leftSnapshot, baseline, baselineDifferences, counters, cancellationToken, progress);
        }

        var completed = DateTimeOffset.UtcNow;
        stopwatch.Stop();

        var summary = new ComparisonSummary
        {
            FilesCompared = counters.FilesCompared,
            FilesEqual = counters.FilesEqual,
            FileDifferences = counters.FileDifferences,
            MissingLeft = counters.MissingLeft,
            MissingRight = counters.MissingRight,
            DirectoryDifferences = counters.DirectoryDifferences,
            Errors = issues.Count,
            BaselineDifferences = counters.BaselineDifferences
        };

        return new ComparisonResult
        {
            Summary = summary,
            Differences = differences.OrderBy(d => d.Path, comparer).ToList(),
            DirectoryDifferences = directoryDifferences.OrderBy(d => d.Path, comparer).ToList(),
            BaselineDifferences = baselineDifferences.OrderBy(d => d.Path, comparer).ToList(),
            Issues = issues,
            Duration = stopwatch.Elapsed,
            StartedAt = started,
            CompletedAt = completed
        };
    }

    private static void CompareDirectories(DirectorySnapshot left, DirectorySnapshot right, ConcurrentBag<ComparisonEntry> output, ComparisonCounters counters, StringComparer comparer)
    {
        var union = new HashSet<string>(left.Directories, comparer);
        union.UnionWith(right.Directories);

        foreach (var path in union)
        {
            if (path == ".")
            {
                continue;
            }

            var hasLeft = left.Directories.Contains(path);
            var hasRight = right.Directories.Contains(path);
            if (hasLeft && hasRight)
            {
                continue;
            }

            counters.DirectoryDifferences++;
            var status = hasLeft ? EntryStatus.MissingRight : EntryStatus.MissingLeft;
            if (hasLeft)
            {
                counters.MissingRight++;
            }
            else
            {
                counters.MissingLeft++;
            }

            output.Add(new ComparisonEntry
            {
                Kind = EntryKind.Directory,
                Status = status,
                Path = path,
                Detail = hasLeft ? "Directory missing on right" : "Directory missing on left"
            });
        }
    }

    private static Task CompareFilesAsync(ComparisonOptions options, DirectorySnapshot left, DirectorySnapshot right, ConcurrentBag<ComparisonEntry> output, ComparisonCounters counters, IProgress<ComparerProgress>? progress, CancellationToken cancellationToken)
    {
        var comparer = options.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        var union = new HashSet<string>(left.Files.Keys, comparer);
        union.UnionWith(right.Files.Keys);
        var total = union.Count;
        var processed = 0;

        return Parallel.ForEachAsync(union, new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, options.MaxDegreeOfParallelism),
            CancellationToken = cancellationToken
        }, (path, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            left.Files.TryGetValue(path, out var leftFile);
            right.Files.TryGetValue(path, out var rightFile);

            if (leftFile is null && rightFile is null)
            {
                return ValueTask.CompletedTask;
            }

            if (leftFile is null)
            {
                Interlocked.Increment(ref counters.MissingLeft);
                Interlocked.Increment(ref counters.FileDifferences);
                output.Add(new ComparisonEntry
                {
                    Kind = EntryKind.File,
                    Status = EntryStatus.MissingLeft,
                    Path = path,
                    Left = null,
                    Right = rightFile is null ? null : CreateMetadata(rightFile, null, ct),
                    Detail = "File missing on left"
                });
            }
            else if (rightFile is null)
            {
                Interlocked.Increment(ref counters.MissingRight);
                Interlocked.Increment(ref counters.FileDifferences);
                output.Add(new ComparisonEntry
                {
                    Kind = EntryKind.File,
                    Status = EntryStatus.MissingRight,
                    Path = path,
                    Left = CreateMetadata(leftFile, null, ct),
                    Right = null,
                    Detail = "File missing on right"
                });
            }
            else
            {
                Interlocked.Increment(ref counters.FilesCompared);

                var leftMeta = CreateMetadata(leftFile, options.Mode == ComparisonMode.Hash ? options.Algorithm : null, ct);
                var rightMeta = CreateMetadata(rightFile, options.Mode == ComparisonMode.Hash ? options.Algorithm : null, ct);

                if (leftFile.Length != rightFile.Length)
                {
                    Interlocked.Increment(ref counters.FileDifferences);
                    output.Add(new ComparisonEntry
                    {
                        Kind = EntryKind.File,
                        Status = EntryStatus.SizeMismatch,
                        Path = path,
                        Left = leftMeta,
                        Right = rightMeta,
                        Detail = $"Size mismatch: {leftFile.Length} vs {rightFile.Length} bytes"
                    });
                }
                else if (options.Mode == ComparisonMode.Hash)
                {
                    if (!string.Equals(leftMeta.Hash, rightMeta.Hash, StringComparison.OrdinalIgnoreCase))
                    {
                        Interlocked.Increment(ref counters.FileDifferences);
                        output.Add(new ComparisonEntry
                        {
                            Kind = EntryKind.File,
                            Status = EntryStatus.HashMismatch,
                            Path = path,
                            Left = leftMeta,
                            Right = rightMeta,
                            Detail = "Hash mismatch"
                        });
                    }
                    else
                    {
                        Interlocked.Increment(ref counters.FilesEqual);
                    }
                }
                else
                {
                    if (options.MtimeTolerance is { } tolerance)
                    {
                        var delta = Math.Abs((leftFile.LastWriteTimeUtc - rightFile.LastWriteTimeUtc).TotalSeconds);
                        if (delta > tolerance.TotalSeconds)
                        {
                            Interlocked.Increment(ref counters.FileDifferences);
                            output.Add(new ComparisonEntry
                            {
                                Kind = EntryKind.File,
                                Status = EntryStatus.TimeMismatch,
                                Path = path,
                                Left = leftMeta,
                                Right = rightMeta,
                                Detail = $"Modified time differs by {delta:F1}s"
                            });
                        }
                        else
                        {
                            Interlocked.Increment(ref counters.FilesEqual);
                        }
                    }
                    else
                    {
                        Interlocked.Increment(ref counters.FilesEqual);
                    }
                }
            }

            var current = Interlocked.Increment(ref processed);
            if (progress is not null && (current % 25 == 0 || current == total))
            {
                progress.Report(new ComparerProgress
                {
                    Stage = "Comparing",
                    Completed = current,
                    Total = total,
                    Message = "Comparing files"
                });
            }
            return ValueTask.CompletedTask;
        });
    }

    private static void CompareWithBaseline(ComparisonOptions options, DirectorySnapshot left, BaselineSnapshot baseline, ConcurrentBag<ComparisonEntry> output, ComparisonCounters counters, CancellationToken cancellationToken, IProgress<ComparerProgress>? progress)
    {
        var comparer = options.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        var union = new HashSet<string>(baseline.Files.Keys, comparer);
        union.UnionWith(left.Files.Keys);
        var total = union.Count;
        var processed = 0;

        Parallel.ForEach(union, new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, options.MaxDegreeOfParallelism),
            CancellationToken = cancellationToken
        }, path =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            baseline.Files.TryGetValue(path, out var baselineFile);
            left.Files.TryGetValue(path, out var leftFile);

            if (baselineFile is not null && leftFile is null)
            {
                Interlocked.Increment(ref counters.BaselineDifferences);
                Interlocked.Increment(ref counters.MissingLeft);
                Interlocked.Increment(ref counters.FileDifferences);
                output.Add(new ComparisonEntry
                {
                    Kind = EntryKind.Baseline,
                    Status = EntryStatus.MissingLeft,
                    Path = path,
                    Left = null,
                    Right = new FileMetadata
                    {
                        Size = baselineFile.Size,
                        LastWriteTimeUtc = baselineFile.LastWriteTimeUtc,
                        Hash = baselineFile.Hash,
                        HashAlgorithm = ParseAlgorithm(baselineFile.HashAlgorithm),
                        Source = "baseline"
                    },
                    Detail = "Missing compared to baseline"
                });
            }
            else if (baselineFile is null && leftFile is not null)
            {
                Interlocked.Increment(ref counters.BaselineDifferences);
                Interlocked.Increment(ref counters.MissingRight);
                Interlocked.Increment(ref counters.FileDifferences);
                output.Add(new ComparisonEntry
                {
                    Kind = EntryKind.Baseline,
                    Status = EntryStatus.MissingRight,
                    Path = path,
                    Left = CreateMetadata(leftFile, options.Mode == ComparisonMode.Hash ? options.Algorithm : null, cancellationToken),
                    Right = null,
                    Detail = "New file not present in baseline"
                });
            }
            else if (baselineFile is not null && leftFile is not null)
            {
                Interlocked.Increment(ref counters.FilesCompared);
                var leftMeta = CreateMetadata(leftFile, options.Mode == ComparisonMode.Hash ? options.Algorithm : null, cancellationToken);
                var rightMeta = new FileMetadata
                {
                    Size = baselineFile.Size,
                    LastWriteTimeUtc = baselineFile.LastWriteTimeUtc,
                    Hash = baselineFile.Hash,
                    HashAlgorithm = ParseAlgorithm(baselineFile.HashAlgorithm),
                    Source = "baseline"
                };

                if (leftFile.Length != baselineFile.Size)
                {
                    Interlocked.Increment(ref counters.FileDifferences);
                    Interlocked.Increment(ref counters.BaselineDifferences);
                    output.Add(new ComparisonEntry
                    {
                        Kind = EntryKind.Baseline,
                        Status = EntryStatus.SizeMismatch,
                        Path = path,
                        Left = leftMeta,
                        Right = rightMeta,
                        Detail = "Size differs from baseline"
                    });
                }
                else if (!CompareHashesIfAvailable(leftMeta, rightMeta))
                {
                    Interlocked.Increment(ref counters.FileDifferences);
                    Interlocked.Increment(ref counters.BaselineDifferences);
                    output.Add(new ComparisonEntry
                    {
                        Kind = EntryKind.Baseline,
                        Status = EntryStatus.HashMismatch,
                        Path = path,
                        Left = leftMeta,
                        Right = rightMeta,
                        Detail = "Hash differs from baseline"
                    });
                }
                else
                {
                    Interlocked.Increment(ref counters.FilesEqual);
                }
            }

            var current = Interlocked.Increment(ref processed);
            if (progress is not null && (current % 25 == 0 || current == total))
            {
                progress.Report(new ComparerProgress
                {
                    Stage = "Baseline",
                    Completed = current,
                    Total = total,
                    Message = "Comparing with baseline"
                });
            }
        });
    }

    private static bool CompareHashesIfAvailable(FileMetadata left, FileMetadata right)
    {
        if (left.Hash is null || right.Hash is null)
        {
            return left.Size == right.Size;
        }

        return string.Equals(left.Hash, right.Hash, StringComparison.OrdinalIgnoreCase);
    }

    private static FileMetadata CreateMetadata(FileEntry entry, HashAlgorithmKind? requestedHash, CancellationToken cancellationToken)
    {
        string? hash = null;
        HashAlgorithmKind? algorithm = null;

        if (requestedHash is { } algo)
        {
            hash = Hashing.ComputeHash(entry.FullPath, algo, cancellationToken);
            algorithm = algo;
        }

        return new FileMetadata
        {
            Size = entry.Length,
            LastWriteTimeUtc = entry.LastWriteTimeUtc,
            Hash = hash,
            HashAlgorithm = algorithm,
            Source = entry.FullPath
        };
    }

    private static HashAlgorithmKind? ParseAlgorithm(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.ToLowerInvariant() switch
        {
            "crc32" => HashAlgorithmKind.Crc32,
            "md5" => HashAlgorithmKind.Md5,
            "sha256" => HashAlgorithmKind.Sha256,
            "xxh64" => HashAlgorithmKind.Xxh64,
            _ => null
        };
    }

    private sealed class ComparisonCounters
    {
        public int FilesCompared;
        public int FilesEqual;
        public int FileDifferences;
        public int MissingLeft;
        public int MissingRight;
        public int DirectoryDifferences;
        public int BaselineDifferences;
    }
}
