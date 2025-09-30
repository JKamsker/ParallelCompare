using System;

namespace FsEqual.Core.FileSystem;

/// <summary>
/// Represents a file system entry exposed through the abstraction layer.
/// </summary>
public interface IFileSystemEntry
{
    /// <summary>
    /// Gets the entry name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the full path to the entry.
    /// </summary>
    string FullPath { get; }

    /// <summary>
    /// Gets the kind of entry represented.
    /// </summary>
    FileSystemEntryType EntryType { get; }

    /// <summary>
    /// Gets a value indicating whether the entry currently exists.
    /// </summary>
    bool Exists { get; }

    /// <summary>
    /// Gets the last write timestamp (UTC).
    /// </summary>
    DateTimeOffset LastWriteTimeUtc { get; }
}
