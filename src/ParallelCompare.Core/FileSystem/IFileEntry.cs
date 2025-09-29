using System.IO;

namespace ParallelCompare.Core.FileSystem;

public interface IFileEntry : IFileSystemEntry
{
    long Length { get; }
    Stream OpenRead();
}
