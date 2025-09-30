namespace ParallelCompare.Core.Options;

/// <summary>
/// Represents the supported hash algorithms for file comparison.
/// </summary>
public enum HashAlgorithmType
{
    Crc32,
    Md5,
    Sha256,
    XxHash64
}
