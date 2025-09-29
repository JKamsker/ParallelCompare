using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.Security.Cryptography;
using ParallelCompare.Core.Options;

namespace ParallelCompare.Core.Comparison.Hashing;

public sealed class FileHashCalculator
{
    public IReadOnlyDictionary<HashAlgorithmType, string> ComputeHashes(
        string filePath,
        IEnumerable<HashAlgorithmType> algorithms,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<HashAlgorithmType, string>();

        foreach (var algorithm in algorithms)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result[algorithm] = ComputeHash(filePath, algorithm);
        }

        return result;
    }

    private string ComputeHash(string filePath, HashAlgorithmType algorithm)
    {
        return algorithm switch
        {
            HashAlgorithmType.Crc32 => ComputeUsingIncremental(filePath, () => new Crc32()),
            HashAlgorithmType.Md5 => ComputeUsingHashAlgorithm(filePath, MD5.Create),
            HashAlgorithmType.Sha256 => ComputeUsingHashAlgorithm(filePath, SHA256.Create),
            HashAlgorithmType.XxHash64 => ComputeUsingIncremental(filePath, () => new XxHash64()),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, null)
        };
    }

    private static string ComputeUsingHashAlgorithm(string filePath, Func<HashAlgorithm> factory)
    {
        using var algorithm = factory();
        using var stream = File.OpenRead(filePath);
        var hash = algorithm.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string ComputeUsingIncremental(string filePath, Func<NonCryptographicHashAlgorithm> factory)
    {
        var algorithm = factory();
        try
        {
            using var stream = File.OpenRead(filePath);
            var buffer = new byte[8192];
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                algorithm.Append(buffer.AsSpan(0, read));
            }

            Span<byte> destination = stackalloc byte[algorithm.HashLengthInBytes];
            algorithm.GetCurrentHash(destination);
            return Convert.ToHexString(destination).ToLowerInvariant();
        }
        finally
        {
            (algorithm as IDisposable)?.Dispose();
        }
    }
}
