using System.Collections.Immutable;
using ParallelCompare.Core.Options;

namespace ParallelCompare.Core.Baselines;

/// <summary>
/// Describes the kinds of entries that can be persisted in a baseline snapshot.
/// </summary>
public enum BaselineEntryType
{
    Directory,
    File
}

/// <summary>
/// Represents a directory or file captured as part of a baseline manifest.
/// </summary>
/// <param name="Name">Entry name relative to its parent.</param>
/// <param name="RelativePath">Path from the baseline root.</param>
/// <param name="EntryType">Indicates whether the entry is a file or directory.</param>
/// <param name="Size">File size in bytes, if applicable.</param>
/// <param name="Modified">Timestamp of the last modification.</param>
/// <param name="Hashes">Hash digests recorded for the entry.</param>
/// <param name="Children">Child entries when the entry is a directory.</param>
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
    /// <summary>
    /// Gets a value indicating whether the entry represents a directory.
    /// </summary>
    public bool IsDirectory => EntryType == BaselineEntryType.Directory;

    /// <summary>
    /// Gets a value indicating whether the entry represents a file.
    /// </summary>
    public bool IsFile => EntryType == BaselineEntryType.File;
}
