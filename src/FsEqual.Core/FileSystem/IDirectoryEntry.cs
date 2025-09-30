using System.Collections.Generic;

namespace FsEqual.Core.FileSystem;

/// <summary>
/// Represents a directory entry that can enumerate children via the file system abstraction.
/// </summary>
public interface IDirectoryEntry : IFileSystemEntry
{
    /// <summary>
    /// Enumerates the immediate child entries for the directory.
    /// </summary>
    /// <returns>The collection of child entries.</returns>
    IEnumerable<IFileSystemEntry> EnumerateEntries();
}
