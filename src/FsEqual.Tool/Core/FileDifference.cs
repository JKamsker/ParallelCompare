namespace FsEqual.Tool.Core;

internal sealed class FileDifference
{
    public FileDifference(
        DifferenceType type,
        string relativePath,
        FileMetadata? left,
        FileMetadata? right,
        string? detail,
        HashAlgorithmKind? algorithm)
    {
        Type = type;
        RelativePath = relativePath;
        Left = left;
        Right = right;
        Detail = detail;
        Algorithm = algorithm;
    }

    public DifferenceType Type { get; }

    public string RelativePath { get; }

    public FileMetadata? Left { get; }

    public FileMetadata? Right { get; }

    public string? Detail { get; }

    public HashAlgorithmKind? Algorithm { get; }
}
