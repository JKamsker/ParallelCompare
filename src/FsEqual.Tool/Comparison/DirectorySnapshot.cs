using System.Collections.Concurrent;
using Spectre.Console;

namespace FsEqual.Tool.Comparison;

internal sealed class DirectorySnapshot
{
    public DirectorySnapshot(string root, IReadOnlyDictionary<string, FileEntry> files, IReadOnlySet<string> directories, IReadOnlyList<ComparisonIssue> issues)
    {
        Root = root;
        Files = files;
        Directories = directories;
        Issues = issues;
    }

    public string Root { get; }
    public IReadOnlyDictionary<string, FileEntry> Files { get; }
    public IReadOnlySet<string> Directories { get; }
    public IReadOnlyList<ComparisonIssue> Issues { get; }

    public static DirectorySnapshot Load(string root, ComparisonOptions options, IgnoreMatcher ignore, CancellationToken cancellationToken)
    {
        var rootFull = Path.GetFullPath(root);
        var comparer = options.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        var files = new ConcurrentDictionary<string, FileEntry>(comparer);
        var directories = new ConcurrentDictionary<string, byte>(comparer);
        var issues = new ConcurrentBag<ComparisonIssue>();

        directories.TryAdd(".", 0);

        void Traverse(string current)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IEnumerable<string> EnumerateDirectoriesSafely()
            {
                try
                {
                    return Directory.EnumerateDirectories(current);
                }
                catch (Exception ex)
                {
                    issues.Add(new ComparisonIssue($"Failed to enumerate directories in '{current}'", ex));
                    return Array.Empty<string>();
                }
            }

            IEnumerable<string> EnumerateFilesSafely()
            {
                try
                {
                    return Directory.EnumerateFiles(current);
                }
                catch (Exception ex)
                {
                    issues.Add(new ComparisonIssue($"Failed to enumerate files in '{current}'", ex));
                    return Array.Empty<string>();
                }
            }

            foreach (var directory in EnumerateDirectoriesSafely())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var rel = Path.GetRelativePath(rootFull, directory);
                if (rel == ".")
                {
                    continue;
                }

                var normalized = Normalize(rel);
                if (ignore.ShouldIgnore(normalized, isDirectory: true))
                {
                    continue;
                }

                try
                {
                    var info = new DirectoryInfo(directory);
                    if (!options.FollowSymlinks && info.LinkTarget is not null)
                    {
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    issues.Add(new ComparisonIssue($"Failed to inspect directory '{directory}'", ex));
                    continue;
                }

                directories.TryAdd(normalized, 0);
                Traverse(directory);
            }

            foreach (var file in EnumerateFilesSafely())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var rel = Path.GetRelativePath(rootFull, file);
                if (rel == ".")
                {
                    continue;
                }

                var normalized = Normalize(rel);
                if (ignore.ShouldIgnore(normalized, isDirectory: false))
                {
                    continue;
                }

                try
                {
                    var info = new FileInfo(file);
                    if (!options.FollowSymlinks && info.LinkTarget is not null)
                    {
                        continue;
                    }

                    files[normalized] = new FileEntry(normalized, file, info.Length, info.LastWriteTimeUtc);
                }
                catch (Exception ex)
                {
                    issues.Add(new ComparisonIssue($"Failed to access file '{file}'", ex));
                }
            }
        }

        Traverse(rootFull);

        return new DirectorySnapshot(rootFull, files, directories.Keys.ToHashSet(comparer), issues.ToList());
    }

    public static string Normalize(string relativePath)
        => relativePath.Replace('\\', '/');
}

internal sealed record FileEntry(string RelativePath, string FullPath, long Length, DateTime LastWriteTimeUtc);
