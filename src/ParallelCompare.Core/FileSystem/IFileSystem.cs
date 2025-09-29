namespace ParallelCompare.Core.FileSystem;

public interface IFileSystem
{
    IDirectoryEntry GetDirectory(string path);
    IFileEntry GetFile(string path);
    bool DirectoryExists(string path);
    bool FileExists(string path);
}
