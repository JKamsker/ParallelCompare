using System.Collections.Generic;

namespace ParallelCompare.Core.FileSystem;

public interface IDirectoryEntry : IFileSystemEntry
{
    IEnumerable<IFileSystemEntry> EnumerateEntries();
}
