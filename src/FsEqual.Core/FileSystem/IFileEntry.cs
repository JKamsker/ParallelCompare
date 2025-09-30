using System.IO;

namespace FsEqual.Core.FileSystem;

/// <summary>
/// Represents a file entry that can be read from the file system abstraction.
/// </summary>
public interface IFileEntry : IFileSystemEntry
{
    /// <summary>
    /// Gets the length of the file in bytes.
    /// </summary>
    long Length { get; }

    /// <summary>
    /// Opens a readable stream for the file.
    /// </summary>
    /// <returns>A read-only stream.</returns>
    Stream OpenRead();
}
