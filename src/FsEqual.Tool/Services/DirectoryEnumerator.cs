using System.Collections.Concurrent;
using FsEqual.Tool.Models;
using Microsoft.Extensions.FileSystemGlobbing;

namespace FsEqual.Tool.Services;

public sealed class DirectoryEnumerator
{
    public async Task<DirectorySnapshot> CaptureAsync(
        string root,
        ComparisonOptions options,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Directory '{root}' does not exist.");
        }

        var snapshot = new DirectorySnapshot(Path.GetFullPath(root), options.CaseSensitive);
        Matcher? ignoreMatcher = null;
        if (options.IgnoreGlobs.Count > 0)
        {
            ignoreMatcher = new Matcher(options.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
            ignoreMatcher.AddInclude("**/*");
            foreach (var pattern in options.IgnoreGlobs)
            {
                ignoreMatcher.AddExclude(pattern);
            }
        }

        var directories = new Queue<string>();
        directories.Enqueue(root);
        var files = new ConcurrentBag<(string FullPath, string RelativePath)>();

        var enumerationOptions = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = false,
            AttributesToSkip = options.FollowSymlinks ? 0 : FileAttributes.ReparsePoint,
            ReturnSpecialDirectories = false,
        };

        while (directories.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = directories.Dequeue();
            var relativeDir = GetRelative(root, current);
            if (!string.IsNullOrEmpty(relativeDir))
            {
                if (IsIgnored(ignoreMatcher, relativeDir, isDirectory: true))
                {
                    continue;
                }
                snapshot.Directories.Add(relativeDir);
            }

            try
            {
                foreach (var entry in Directory.EnumerateFileSystemEntries(current, "*", enumerationOptions))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var attributes = File.GetAttributes(entry);
                    var relativePath = GetRelative(root, entry);
                    var isDirectory = attributes.HasFlag(FileAttributes.Directory);
                    if (IsIgnored(ignoreMatcher, relativePath, isDirectory))
                    {
                        continue;
                    }

                    if (isDirectory)
                    {
                        snapshot.Directories.Add(relativePath);
                        directories.Enqueue(entry);
                    }
                    else
                    {
                        files.Add((entry, relativePath));
                    }
                }
            }
            catch (Exception ex)
            {
                snapshot.Errors.Add(new ComparisonError(relativeDir, ex.Message, ex));
            }
        }

        var parallelOptions = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = options.Threads <= 0 ? Environment.ProcessorCount : options.Threads,
        };

        await Parallel.ForEachAsync(files, parallelOptions, async (item, token) =>
        {
            try
            {
                var info = new FileInfo(item.FullPath);
                var metadata = new FileMetadata(item.RelativePath, info.Length, info.LastWriteTimeUtc);
                snapshot.Files[item.RelativePath] = metadata;
            }
            catch (Exception ex)
            {
                snapshot.Errors.Add(new ComparisonError(item.RelativePath, ex.Message, ex));
            }

            await ValueTask.CompletedTask;
        });

        return snapshot;
    }

    private static string GetRelative(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        if (relative == "." || string.IsNullOrEmpty(relative))
        {
            return string.Empty;
        }

        return relative.Replace(Path.DirectorySeparatorChar, '/');
    }

    private static bool IsIgnored(Matcher? matcher, string relativePath, bool isDirectory)
    {
        if (matcher == null)
        {
            return false;
        }

        var candidate = isDirectory ? EnsureTrailingSlash(relativePath) : relativePath;
        return !matcher.Match(candidate).HasMatches;
    }

    private static string EnsureTrailingSlash(string text)
    {
        if (text.EndsWith("/", StringComparison.Ordinal))
        {
            return text;
        }

        return text + "/";
    }
}
