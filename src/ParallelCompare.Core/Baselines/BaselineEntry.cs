using System.Collections.Immutable;
using ParallelCompare.Core.Options;

namespace ParallelCompare.Core.Baselines;

public enum BaselineEntryType
{
    Directory,
    File
}

public sealed record BaselineEntry(
    string Name,
    string RelativePath,
    BaselineEntryType EntryType,
    long? Size,
    DateTimeOffset? Modified,
    ImmutableDictionary<HashAlgorithmType, string> Hashes,
    ImmutableArray<BaselineEntry> Children
)
{
    public bool IsDirectory => EntryType == BaselineEntryType.Directory;
    public bool IsFile => EntryType == BaselineEntryType.File;
}
