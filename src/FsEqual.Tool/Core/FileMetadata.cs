namespace FsEqual.Tool.Core;

internal sealed class FileMetadata
{
    public FileMetadata(string fullPath, long length, DateTimeOffset lastWriteTime, bool isDirectory, bool isSymlink)
    {
        FullPath = fullPath;
        Length = length;
        LastWriteTime = lastWriteTime;
        IsDirectory = isDirectory;
        IsSymlink = isSymlink;
    }

    public string FullPath { get; }

    public long Length { get; }

    public DateTimeOffset LastWriteTime { get; }

    public bool IsDirectory { get; }

    public bool IsSymlink { get; }

    public string? Hash { get; set; }
}
