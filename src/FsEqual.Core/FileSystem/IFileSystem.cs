namespace FsEqual.Core.FileSystem;

/// <summary>
/// Provides access to directories and files via an abstracted file system.
/// </summary>
public interface IFileSystem
{
    /// <summary>
    /// Gets a directory entry for the specified path.
    /// </summary>
    /// <param name="path">Directory path.</param>
    /// <returns>The directory entry.</returns>
    IDirectoryEntry GetDirectory(string path);

    /// <summary>
    /// Gets a file entry for the specified path.
    /// </summary>
    /// <param name="path">File path.</param>
    /// <returns>The file entry.</returns>
    IFileEntry GetFile(string path);

    /// <summary>
    /// Determines whether a directory exists at the specified path.
    /// </summary>
    /// <param name="path">Directory path.</param>
    /// <returns><c>true</c> if the directory exists; otherwise, <c>false</c>.</returns>
    bool DirectoryExists(string path);

    /// <summary>
    /// Determines whether a file exists at the specified path.
    /// </summary>
    /// <param name="path">File path.</param>
    /// <returns><c>true</c> if the file exists; otherwise, <c>false</c>.</returns>
    bool FileExists(string path);
}
