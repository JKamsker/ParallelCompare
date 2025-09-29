using System.Buffers;
using System.Security.Cryptography;
using System.IO.Hashing;

namespace FsEqual.Core;

public sealed class HashComputer
{
    private readonly HashAlgorithmKind _algorithm;

    public HashComputer(HashAlgorithmKind algorithm)
    {
        _algorithm = algorithm;
    }

    public string ComputeHash(string filePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _algorithm switch
        {
            HashAlgorithmKind.Md5 => Compute(filePath, () => MD5.Create(), cancellationToken),
            HashAlgorithmKind.Sha256 => Compute(filePath, () => SHA256.Create(), cancellationToken),
            HashAlgorithmKind.Crc32 => ComputeChecksum(filePath, () => new Crc32(), cancellationToken),
            HashAlgorithmKind.Xxh64 => ComputeChecksum(filePath, () => new XxHash64(), cancellationToken),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private static string Compute(string filePath, Func<HashAlgorithm> factory, CancellationToken cancellationToken)
    {
        using var stream = File.OpenRead(filePath);
        using var algorithm = factory();
        var hash = algorithm.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string ComputeChecksum(string filePath, Func<NonCryptographicHashAlgorithm> factory, CancellationToken cancellationToken)
    {
        var algorithm = factory();
        using var stream = File.OpenRead(filePath);
        var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        try
        {
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                algorithm.Append(new ReadOnlySpan<byte>(buffer, 0, read));
            }
            var hash = algorithm.GetCurrentHash();
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            if (algorithm is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
