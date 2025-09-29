using System.Collections.Concurrent;
using System.Diagnostics;
using FsEqual.Tool.Models;

namespace FsEqual.Tool.Services;

public sealed class DirectoryComparator
{
    public async Task<ComparisonReport> CompareAsync(
        DirectorySnapshot left,
        DirectorySnapshot? right,
        ComparisonOptions options,
        HashAlgorithmKind algorithm,
        CancellationToken cancellationToken,
        IProgress<(string Stage, int Value, int Total)>? progress = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var comparer = options.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        var errors = new ConcurrentBag<ComparisonError>();
        foreach (var error in left.Errors)
        {
            errors.Add(error);
        }

        if (right != null)
        {
            foreach (var error in right.Errors)
            {
                errors.Add(error);
            }
        }

        progress?.Report(("hash", 0, 0));

        if (options.Mode == ComparisonMode.Hash)
        {
            await ComputeHashesAsync(left, algorithm, options, cancellationToken, errors, progress);
            if (right != null)
            {
                await ComputeHashesAsync(right, algorithm, options, cancellationToken, errors, progress);
            }
        }

        var allDirs = new SortedSet<string>(comparer);
        foreach (var dir in left.Directories)
        {
            allDirs.Add(dir);
        }

        if (right != null)
        {
            foreach (var dir in right.Directories)
            {
                allDirs.Add(dir);
            }
        }

        var allFiles = new SortedSet<string>(comparer);
        foreach (var file in left.Files.Keys)
        {
            allFiles.Add(file);
        }

        if (right != null)
        {
            foreach (var file in right.Files.Keys)
            {
                allFiles.Add(file);
            }
        }

        var results = new List<PathComparison>();
        int equalCount = 0;
        int diffCount = 0;
        int missingLeft = 0;
        int missingRight = 0;

        foreach (var dir in allDirs)
        {
            var leftExists = left.Directories.Contains(dir);
            var rightExists = right?.Directories.Contains(dir) ?? false;
            var status = DetermineDirectoryStatus(leftExists, rightExists);
            if (status == ComparisonStatus.Equal)
            {
                equalCount++;
            }
            else
            {
                diffCount++;
                if (status == ComparisonStatus.MissingLeft)
                {
                    missingLeft++;
                }
                else if (status == ComparisonStatus.MissingRight)
                {
                    missingRight++;
                }
            }

            results.Add(new PathComparison(dir, PathKind.Directory, status, null, null));
        }

        progress?.Report(("files", 0, allFiles.Count));
        var index = 0;
        foreach (var path in allFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(("files", ++index, allFiles.Count));
            left.Files.TryGetValue(path, out var leftFile);
            FileMetadata? rightFile = null;
            if (right != null)
            {
                right.Files.TryGetValue(path, out rightFile);
            }
            var evaluation = EvaluateFile(left, right, leftFile, rightFile, options, algorithm);

            switch (evaluation.Status)
            {
                case ComparisonStatus.Equal:
                    equalCount++;
                    break;
                case ComparisonStatus.MissingLeft:
                    missingLeft++;
                    diffCount++;
                    break;
                case ComparisonStatus.MissingRight:
                    missingRight++;
                    diffCount++;
                    break;
                case ComparisonStatus.Error:
                    diffCount++;
                    break;
                default:
                    diffCount++;
                    break;
            }

            results.Add(evaluation);
        }

        var summary = new ComparisonSummary
        {
            TotalItems = results.Count,
            Directories = allDirs.Count,
            Files = allFiles.Count,
            Equal = equalCount,
            Differences = diffCount,
            MissingLeft = missingLeft,
            MissingRight = missingRight,
            Errors = errors.Count,
            Duration = stopwatch.Elapsed,
        };

        var report = new ComparisonReport
        {
            Items = results,
            Summary = summary,
            Errors = errors.ToList(),
            LeftRoot = left.Root,
            RightRoot = right?.Root,
            Mode = options.Mode,
            Algorithm = algorithm,
        };

        stopwatch.Stop();
        return report;
    }

    private static ComparisonStatus DetermineDirectoryStatus(bool leftExists, bool rightExists)
    {
        if (leftExists && rightExists)
        {
            return ComparisonStatus.Equal;
        }

        if (leftExists)
        {
            return ComparisonStatus.MissingRight;
        }

        return ComparisonStatus.MissingLeft;
    }

    private static PathComparison EvaluateFile(
        DirectorySnapshot left,
        DirectorySnapshot? rightSnapshot,
        FileMetadata? leftFile,
        FileMetadata? rightFile,
        ComparisonOptions options,
        HashAlgorithmKind algorithm)
    {
        if (leftFile == null && rightFile == null)
        {
            return new PathComparison(string.Empty, PathKind.File, ComparisonStatus.Error, null, null, "Unexpected state");
        }

        if (leftFile == null)
        {
            return new PathComparison(rightFile!.RelativePath, PathKind.File, ComparisonStatus.MissingLeft, null, rightFile, "Missing in left directory");
        }

        if (rightFile == null)
        {
            return new PathComparison(leftFile.RelativePath, PathKind.File, ComparisonStatus.MissingRight, leftFile, null, "Missing in right directory");
        }

        if (options.Mode == ComparisonMode.Hash)
        {
            if (string.IsNullOrEmpty(leftFile.Hash) || leftFile.HashAlgorithm != algorithm)
            {
                return new PathComparison(leftFile.RelativePath, PathKind.File, ComparisonStatus.Error, leftFile, rightFile, "Hash not computed for left file");
            }

            if (string.IsNullOrEmpty(rightFile.Hash) || rightFile.HashAlgorithm != algorithm)
            {
                return new PathComparison(leftFile.RelativePath, PathKind.File, ComparisonStatus.Error, leftFile, rightFile, "Hash not computed for right file");
            }

            if (leftFile.Hash.Equals(rightFile.Hash, options.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase))
            {
                return new PathComparison(leftFile.RelativePath, PathKind.File, ComparisonStatus.Equal, leftFile, rightFile);
            }

            return new PathComparison(leftFile.RelativePath, PathKind.File, ComparisonStatus.HashMismatch, leftFile, rightFile, "Hash mismatch");
        }

        if (leftFile.Size != rightFile.Size)
        {
            return new PathComparison(leftFile.RelativePath, PathKind.File, ComparisonStatus.SizeMismatch, leftFile, rightFile, "File sizes differ");
        }

        if (options.MtimeToleranceSeconds.HasValue)
        {
            var delta = Math.Abs((leftFile.LastWriteTimeUtc - rightFile.LastWriteTimeUtc).TotalSeconds);
            if (delta > options.MtimeToleranceSeconds.Value)
            {
                return new PathComparison(leftFile.RelativePath, PathKind.File, ComparisonStatus.MetadataMismatch, leftFile, rightFile, "Modified time difference exceeds tolerance");
            }
        }
        else if (leftFile.LastWriteTimeUtc != rightFile.LastWriteTimeUtc)
        {
            return new PathComparison(leftFile.RelativePath, PathKind.File, ComparisonStatus.MetadataMismatch, leftFile, rightFile, "Modified time differs");
        }

        return new PathComparison(leftFile.RelativePath, PathKind.File, ComparisonStatus.Equal, leftFile, rightFile);
    }

    internal static async Task ComputeHashesAsync(
        DirectorySnapshot snapshot,
        HashAlgorithmKind algorithm,
        ComparisonOptions options,
        CancellationToken cancellationToken,
        ConcurrentBag<ComparisonError> errors,
        IProgress<(string Stage, int Value, int Total)>? progress)
    {
        var filesToHash = snapshot.Files.Values
            .Where(file => string.IsNullOrEmpty(file.Hash) || file.HashAlgorithm != algorithm)
            .ToList();

        if (filesToHash.Count == 0)
        {
            return;
        }

        var parallelOptions = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = options.Threads <= 0 ? Environment.ProcessorCount : options.Threads,
        };

        var index = 0;
        progress?.Report(("hash", index, filesToHash.Count));
        await Parallel.ForEachAsync(filesToHash, parallelOptions, async (metadata, token) =>
        {
            try
            {
                var fullPath = Path.Combine(snapshot.Root, metadata.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(fullPath))
                {
                    errors.Add(new ComparisonError(metadata.RelativePath, $"File not found while hashing: {fullPath}"));
                    return;
                }

                var hash = await HashCalculator.ComputeAsync(fullPath, algorithm, token);
                metadata.SetHash(hash, algorithm);
            }
            catch (Exception ex)
            {
                errors.Add(new ComparisonError(metadata.RelativePath, ex.Message, ex));
            }
            finally
            {
                var current = Interlocked.Increment(ref index);
                progress?.Report(("hash", current, filesToHash.Count));
            }
        });
    }
}
