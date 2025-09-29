namespace FsEqual.Tool.Models;

public sealed class DirectorySnapshot
{
    public DirectorySnapshot(string root, bool caseSensitive)
    {
        Root = root;
        var comparer = caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        Files = new Dictionary<string, FileMetadata>(comparer);
        Directories = new HashSet<string>(comparer);
    }

    public string Root { get; }

    public Dictionary<string, FileMetadata> Files { get; }

    public HashSet<string> Directories { get; }

    public List<ComparisonError> Errors { get; } = new();
}
