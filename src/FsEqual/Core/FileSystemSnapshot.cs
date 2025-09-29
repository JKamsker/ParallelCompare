using System.Collections.Immutable;
using Microsoft.Extensions.FileSystemGlobbing;

namespace FsEqual.Core;

public sealed class FileSystemSnapshot
{
    private readonly Dictionary<string, SnapshotEntry> _entries;

    private FileSystemSnapshot(Dictionary<string, SnapshotEntry> entries, StringComparer comparer)
    {
        _entries = entries;
        Comparer = comparer;
    }

    public IReadOnlyDictionary<string, SnapshotEntry> Entries => _entries;
    public IEnumerable<string> Paths => _entries.Keys;
    public StringComparer Comparer { get; }
    public bool TryGet(string relativePath, out SnapshotEntry entry) => _entries.TryGetValue(relativePath, out entry!);

    public static FileSystemSnapshot Create(
        string root,
        bool caseSensitive,
        bool followSymlinks,
        ImmutableArray<string> ignoreGlobs,
        CancellationToken cancellationToken)
    {
        var comparer = caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        var entries = new Dictionary<string, SnapshotEntry>(comparer);

        Matcher? matcher = null;
        if (ignoreGlobs.Length > 0)
        {
            matcher = new Matcher(caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
            matcher.AddInclude("**/*");
            foreach (var pattern in ignoreGlobs)
            {
                matcher.AddExclude(pattern);
            }
        }

        var stack = new Stack<(string Absolute, string Relative)>();
        stack.Push((root, string.Empty));

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = stack.Pop();
            var info = new DirectoryInfo(current.Absolute);
            if (!info.Exists)
            {
                continue;
            }

            foreach (var entry in info.EnumerateFileSystemInfos())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!followSymlinks && entry.LinkTarget is not null)
                {
                    continue;
                }

                var relative = Path.Combine(current.Relative, entry.Name);
                var normalizedRelative = Normalize(relative);
                if (matcher is not null && !matcher.Match(normalizedRelative).HasMatches)
                {
                    continue;
                }

                if ((entry.Attributes & FileAttributes.Directory) == FileAttributes.Directory)
                {
                    var dirEntry = new SnapshotEntry(normalizedRelative, true, 0, entry.LastWriteTimeUtc);
                    entries[normalizedRelative] = dirEntry;
                    stack.Push((entry.FullName, relative));
                }
                else
                {
                    var fileInfo = new FileInfo(entry.FullName);
                    var fileEntry = new SnapshotEntry(normalizedRelative, false, fileInfo.Length, entry.LastWriteTimeUtc);
                    entries[normalizedRelative] = fileEntry;
                }
            }
        }

        return new FileSystemSnapshot(entries, comparer);
    }

    private static string Normalize(string path)
    {
        return path.Replace(Path.DirectorySeparatorChar, '/');
    }
}

public sealed record SnapshotEntry(string RelativePath, bool IsDirectory, long Size, DateTime LastWriteTimeUtc)
{
    public string RelativePath { get; init; } = RelativePath;
    public bool IsDirectory { get; init; } = IsDirectory;
    public long Size { get; init; } = Size;
    public DateTime LastWriteTimeUtc { get; init; } = LastWriteTimeUtc;
}
