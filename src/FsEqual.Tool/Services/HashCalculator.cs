using System.Buffers;
using System.IO.Hashing;
using System.Security.Cryptography;
using FsEqual.Tool.Models;

namespace FsEqual.Tool.Services;

public static class HashCalculator
{
    public static async Task<string> ComputeAsync(string path, HashAlgorithmKind algorithm, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 8192, useAsync: true);
        return algorithm switch
        {
            HashAlgorithmKind.Crc32 => await ComputeCrc32Async(stream, cancellationToken),
            HashAlgorithmKind.Md5 => await ComputeMd5Async(stream, cancellationToken),
            HashAlgorithmKind.Sha256 => await ComputeSha256Async(stream, cancellationToken),
            HashAlgorithmKind.Xxh64 => await ComputeXxh64Async(stream, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, null),
        };
    }

    private static async Task<string> ComputeCrc32Async(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(8192);
        try
        {
            var hash = new Crc32();
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
            {
                hash.Append(buffer.AsSpan(0, bytesRead));
            }

            Span<byte> result = stackalloc byte[hash.HashLengthInBytes];
            hash.GetCurrentHash(result);
            return Convert.ToHexString(result).ToLowerInvariant();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static async Task<string> ComputeMd5Async(Stream stream, CancellationToken cancellationToken)
    {
        using var md5 = MD5.Create();
        var hash = await md5.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static async Task<string> ComputeSha256Async(Stream stream, CancellationToken cancellationToken)
    {
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static async Task<string> ComputeXxh64Async(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(8192);
        try
        {
            var hash = new XxHash64();
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
            {
                hash.Append(buffer.AsSpan(0, bytesRead));
            }

            Span<byte> result = stackalloc byte[hash.HashLengthInBytes];
            hash.GetCurrentHash(result);
            return Convert.ToHexString(result).ToLowerInvariant();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
