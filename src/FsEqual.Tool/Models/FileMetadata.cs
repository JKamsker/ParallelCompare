using FsEqual.Tool.Models;

namespace FsEqual.Tool.Models;

public sealed class FileMetadata
{
    public FileMetadata(string relativePath, long size, DateTimeOffset lastWriteTimeUtc)
    {
        RelativePath = relativePath;
        Size = size;
        LastWriteTimeUtc = lastWriteTimeUtc;
    }

    public string RelativePath { get; }

    public long Size { get; }

    public DateTimeOffset LastWriteTimeUtc { get; }

    public string? Hash { get; private set; }

    public HashAlgorithmKind? HashAlgorithm { get; private set; }

    public void SetHash(string hash, HashAlgorithmKind algorithm)
    {
        Hash = hash;
        HashAlgorithm = algorithm;
    }
}
