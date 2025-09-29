using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Hashing;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.Extensions.FileSystemGlobbing;

namespace FsEqual.Tool.Core;

internal sealed class DirectoryComparator
{
    private readonly ConsoleLogger _logger;

    public DirectoryComparator(ConsoleLogger logger)
    {
        _logger = logger;
    }

    public async Task<ComparisonResult> CompareAsync(ComparisonOptions options, bool showProgress, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var comparer = options.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        var matcher = BuildMatcher(options);
        var errors = new ConcurrentBag<ComparisonError>();

        if (showProgress)
        {
            _logger.Log(VerbosityLevel.Info, "Enumerating left side ...");
        }

        var left = await Task.Run(() => Enumerate(options.LeftRoot, matcher, comparer, options.FollowSymlinks, errors, cancellationToken), cancellationToken);

        if (showProgress)
        {
            _logger.Log(VerbosityLevel.Info, "Enumerating right side ...");
        }

        var right = await Task.Run(() => Enumerate(options.RightRoot, matcher, comparer, options.FollowSymlinks, errors, cancellationToken), cancellationToken);

        if (showProgress)
        {
            _logger.Log(VerbosityLevel.Info, "Analyzing differences ...");
        }

        var summary = new ComparisonSummary();
        var differences = new List<FileDifference>();

        var allPaths = new HashSet<string>(comparer);
        allPaths.UnionWith(left.Files.Keys);
        allPaths.UnionWith(right.Files.Keys);
        allPaths.UnionWith(left.Directories.Keys);
        allPaths.UnionWith(right.Directories.Keys);

        var hashPairs = new List<(string Path, FileMetadata Left, FileMetadata Right)>();

        foreach (var path in allPaths.OrderBy(p => p, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var leftFileExists = left.Files.TryGetValue(path, out var leftFile);
            var rightFileExists = right.Files.TryGetValue(path, out var rightFile);
            var leftDirExists = left.Directories.TryGetValue(path, out var leftDir);
            var rightDirExists = right.Directories.TryGetValue(path, out var rightDir);

            if (leftDirExists && rightFileExists || leftFileExists && rightDirExists)
            {
                summary.TypeMismatches++;
                differences.Add(new FileDifference(DifferenceType.TypeMismatch, path, leftFile ?? leftDir, rightFile ?? rightDir, "File/Directory mismatch", null));
                continue;
            }

            if (leftDirExists && !rightDirExists)
            {
                summary.DirectoriesCompared++;
                summary.MissingRight++;
                differences.Add(new FileDifference(DifferenceType.MissingRight, path, leftDir!, null, "Directory missing on right", null));
                continue;
            }

            if (!leftDirExists && rightDirExists)
            {
                summary.DirectoriesCompared++;
                summary.MissingLeft++;
                differences.Add(new FileDifference(DifferenceType.MissingLeft, path, null, rightDir!, "Directory missing on left", null));
                continue;
            }

            if (leftDirExists && rightDirExists)
            {
                summary.DirectoriesCompared++;
                summary.EqualDirectories++;
                continue;
            }

            if (leftFileExists && !rightFileExists)
            {
                summary.FilesCompared++;
                summary.MissingRight++;
                differences.Add(new FileDifference(DifferenceType.MissingRight, path, leftFile!, null, "File missing on right", null));
                continue;
            }

            if (!leftFileExists && rightFileExists)
            {
                summary.FilesCompared++;
                summary.MissingLeft++;
                differences.Add(new FileDifference(DifferenceType.MissingLeft, path, null, rightFile!, "File missing on left", null));
                continue;
            }

            if (!leftFileExists || !rightFileExists)
            {
                continue;
            }

            if (leftFile is null || rightFile is null)
            {
                continue;
            }

            summary.FilesCompared++;

            if (options.Mode == ComparisonMode.Quick)
            {
                var sizeEqual = leftFile.Length == rightFile.Length;
                var mtimeDiff = Math.Abs((leftFile.LastWriteTime - rightFile.LastWriteTime).TotalSeconds);

                if (!sizeEqual)
                {
                    summary.SizeMismatches++;
                    differences.Add(new FileDifference(DifferenceType.SizeMismatch, path, leftFile, rightFile, $"Lengths differ ({leftFile.Length} vs {rightFile.Length})", null));
                    continue;
                }

                if (mtimeDiff > options.MtimeToleranceSeconds)
                {
                    summary.MetadataMismatches++;
                    differences.Add(new FileDifference(DifferenceType.MetadataMismatch, path, leftFile, rightFile, $"MTime delta {mtimeDiff:F2}s exceeds tolerance", null));
                    continue;
                }

                summary.EqualFiles++;
            }
            else
            {
                if (leftFile.Length != rightFile.Length)
                {
                    summary.SizeMismatches++;
                    differences.Add(new FileDifference(DifferenceType.SizeMismatch, path, leftFile, rightFile, $"Lengths differ ({leftFile.Length} vs {rightFile.Length})", options.Algorithm));
                    continue;
                }

                hashPairs.Add((path, leftFile, rightFile));
            }
        }

        if (options.Mode == ComparisonMode.Hash && hashPairs.Count > 0)
        {
            if (showProgress)
            {
                _logger.Log(VerbosityLevel.Info, $"Hashing {hashPairs.Count} file pair(s) using {options.Algorithm} ...");
            }

            await HashPairsAsync(hashPairs, options, summary, differences, errors, cancellationToken);
        }

        summary.ErrorCount = errors.Count;

        var outcome = DetermineOutcome(summary, errors.Count);
        stopwatch.Stop();

        return new ComparisonResult(outcome, summary, differences.OrderBy(d => d.RelativePath, comparer).ToArray(), errors.ToArray(), stopwatch.Elapsed);
    }

    private static async Task HashPairsAsync(
        IReadOnlyList<(string Path, FileMetadata Left, FileMetadata Right)> pairs,
        ComparisonOptions options,
        ComparisonSummary summary,
        List<FileDifference> differences,
        ConcurrentBag<ComparisonError> errors,
        CancellationToken cancellationToken)
    {
        var diffBag = new ConcurrentBag<FileDifference>();
        var equalCount = 0;
        var mismatchCount = 0;

        await Parallel.ForEachAsync(pairs, new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, options.MaxDegreeOfParallelism),
            CancellationToken = cancellationToken,
        }, async (pair, token) =>
        {
            try
            {
                var leftHash = await Task.Run(() => ComputeHash(pair.Left.FullPath, options.Algorithm, token), token);
                var rightHash = await Task.Run(() => ComputeHash(pair.Right.FullPath, options.Algorithm, token), token);
                pair.Left.Hash = leftHash;
                pair.Right.Hash = rightHash;

                if (!string.Equals(leftHash, rightHash, StringComparison.OrdinalIgnoreCase))
                {
                    diffBag.Add(new FileDifference(DifferenceType.HashMismatch, pair.Path, pair.Left, pair.Right, $"Hash differs ({leftHash} vs {rightHash})", options.Algorithm));
                    Interlocked.Increment(ref mismatchCount);
                }
                else
                {
                    Interlocked.Increment(ref equalCount);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                errors.Add(new ComparisonError(pair.Path, ex.Message));
            }
        });

        summary.EqualFiles += equalCount;
        summary.HashMismatches += mismatchCount;

        if (!diffBag.IsEmpty)
        {
            lock (differences)
            {
                differences.AddRange(diffBag);
            }
        }
    }

    private static string ComputeHash(string path, HashAlgorithmKind algorithm, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        return algorithm switch
        {
            HashAlgorithmKind.Crc32 => ComputeWithBuffer(path, algorithm, token),
            HashAlgorithmKind.Xxh64 => ComputeWithBuffer(path, algorithm, token),
            HashAlgorithmKind.Md5 => ComputeWithManaged(path, static s => MD5.HashData(s)),
            HashAlgorithmKind.Sha256 => ComputeWithManaged(path, static s => SHA256.HashData(s)),
            _ => throw new NotSupportedException($"Algorithm {algorithm} is not supported"),
        };
    }

    private static string ComputeWithManaged(string path, Func<Stream, byte[]> hashFactory)
    {
        using var stream = OpenStream(path);
        return Convert.ToHexString(hashFactory(stream));
    }

    private static string ComputeWithBuffer(string path, HashAlgorithmKind algorithm, CancellationToken token)
    {
        const int bufferSize = 128 * 1024;
        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        try
        {
            using var stream = OpenStream(path);
            return algorithm switch
            {
                HashAlgorithmKind.Crc32 => ComputeCrc32(stream, buffer, token),
                HashAlgorithmKind.Xxh64 => ComputeXxHash64(stream, buffer, token),
                _ => throw new NotSupportedException($"Algorithm {algorithm} is not supported"),
            };
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static FileStream OpenStream(string path)
    {
        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024, FileOptions.SequentialScan);
    }

    private static string ComputeCrc32(Stream stream, byte[] buffer, CancellationToken token)
    {
        var crc = new Crc32();
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var read = stream.Read(buffer, 0, buffer.Length);
            if (read == 0)
            {
                break;
            }

            crc.Append(new ReadOnlySpan<byte>(buffer, 0, read));
        }

        return Convert.ToHexString(crc.GetCurrentHash());
    }

    private static string ComputeXxHash64(Stream stream, byte[] buffer, CancellationToken token)
    {
        var hash = new XxHash64();
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var read = stream.Read(buffer, 0, buffer.Length);
            if (read == 0)
            {
                break;
            }

            hash.Append(new ReadOnlySpan<byte>(buffer, 0, read));
        }

        return Convert.ToHexString(hash.GetCurrentHash());
    }

    private static Matcher? BuildMatcher(ComparisonOptions options)
    {
        if (options.IgnoreGlobs.Count == 0)
        {
            return null;
        }

        var matcher = new Matcher(options.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
        foreach (var glob in options.IgnoreGlobs)
        {
            if (!string.IsNullOrWhiteSpace(glob))
            {
                matcher.AddInclude(glob);
            }
        }

        return matcher;
    }

    private static EnumerationResult Enumerate(
        string root,
        Matcher? matcher,
        StringComparer comparer,
        bool followSymlinks,
        ConcurrentBag<ComparisonError> errors,
        CancellationToken cancellationToken)
    {
        var files = new Dictionary<string, FileMetadata>(comparer);
        var directories = new Dictionary<string, FileMetadata>(comparer);
        var stack = new Stack<string>();
        var rootFull = Path.GetFullPath(root);
        stack.Push(rootFull);

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = stack.Pop();

            try
            {
                foreach (var entry in Directory.EnumerateFileSystemEntries(current))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var rel = Path.GetRelativePath(rootFull, entry);
                    if (string.IsNullOrEmpty(rel) || rel == ".")
                    {
                        continue;
                    }

                    var normalized = Normalize(rel);
                    var attributes = File.GetAttributes(entry);
                    var isDirectory = attributes.HasFlag(FileAttributes.Directory);
                    var isSymlink = attributes.HasFlag(FileAttributes.ReparsePoint);

                    if (ShouldIgnore(matcher, normalized, isDirectory))
                    {
                        continue;
                    }

                    if (isDirectory)
                    {
                        var info = new DirectoryInfo(entry);
                        directories[normalized] = new FileMetadata(entry, 0, info.LastWriteTimeUtc, true, isSymlink);
                        if (followSymlinks || !isSymlink)
                        {
                            stack.Push(entry);
                        }
                    }
                    else
                    {
                        var info = new FileInfo(entry);
                        files[normalized] = new FileMetadata(entry, info.Length, info.LastWriteTimeUtc, false, isSymlink);
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add(new ComparisonError(current, ex.Message));
            }
        }

        return new EnumerationResult(files, directories);
    }

    private static string Normalize(string relativePath)
    {
        return relativePath.Replace('\\', '/');
    }

    private static bool ShouldIgnore(Matcher? matcher, string path, bool isDirectory)
    {
        if (matcher == null)
        {
            return false;
        }

        if (matcher.Match(path).HasMatches)
        {
            return true;
        }

        if (isDirectory)
        {
            var alt = path.EndsWith('/') ? path : path + "/";
            if (matcher.Match(alt).HasMatches)
            {
                return true;
            }
        }

        return false;
    }

    private static ComparisonOutcome DetermineOutcome(ComparisonSummary summary, int errorCount)
    {
        if (errorCount > 0)
        {
            return ComparisonOutcome.Errors;
        }

        if (summary.MissingLeft > 0 || summary.MissingRight > 0 || summary.TypeMismatches > 0 || summary.SizeMismatches > 0 || summary.HashMismatches > 0 || summary.MetadataMismatches > 0)
        {
            return ComparisonOutcome.Differences;
        }

        return ComparisonOutcome.Equal;
    }

    private sealed record EnumerationResult(
        IReadOnlyDictionary<string, FileMetadata> Files,
        IReadOnlyDictionary<string, FileMetadata> Directories);
}
