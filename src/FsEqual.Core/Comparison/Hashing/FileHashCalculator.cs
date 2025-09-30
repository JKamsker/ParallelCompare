using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.Security.Cryptography;
using FsEqual.Core.FileSystem;
using FsEqual.Core.Options;

namespace FsEqual.Core.Comparison.Hashing;

public sealed class FileHashCalculator
{
    public IReadOnlyDictionary<HashAlgorithmType, string> ComputeHashes(
        IFileEntry file,
        IEnumerable<HashAlgorithmType> algorithms,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<HashAlgorithmType, string>();

        foreach (var algorithm in algorithms)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result[algorithm] = ComputeHash(file, algorithm);
        }

        return result;
    }

    private string ComputeHash(IFileEntry file, HashAlgorithmType algorithm)
    {
        return algorithm switch
        {
            HashAlgorithmType.Crc32 => ComputeUsingIncremental(file, () => new Crc32()),
            HashAlgorithmType.Md5 => ComputeUsingHashAlgorithm(file, MD5.Create),
            HashAlgorithmType.Sha256 => ComputeUsingHashAlgorithm(file, SHA256.Create),
            HashAlgorithmType.XxHash64 => ComputeUsingIncremental(file, () => new XxHash64()),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, null)
        };
    }

    private static string ComputeUsingHashAlgorithm(IFileEntry file, Func<HashAlgorithm> factory)
    {
        using var algorithm = factory();
        using var stream = file.OpenRead();
        var hash = algorithm.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string ComputeUsingIncremental(IFileEntry file, Func<NonCryptographicHashAlgorithm> factory)
    {
        var algorithm = factory();
        try
        {
            using var stream = file.OpenRead();
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
