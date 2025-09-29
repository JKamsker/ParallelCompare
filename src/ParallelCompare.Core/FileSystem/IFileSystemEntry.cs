using System;

namespace ParallelCompare.Core.FileSystem;

public interface IFileSystemEntry
{
    string Name { get; }
    string FullPath { get; }
    FileSystemEntryType EntryType { get; }
    bool Exists { get; }
    DateTimeOffset LastWriteTimeUtc { get; }
}
