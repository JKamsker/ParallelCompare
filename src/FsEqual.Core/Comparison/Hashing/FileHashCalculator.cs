using System;
using System.Buffers;
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
        CancellationToken cancellationToken,
        IComparisonProgressSink? progressSink = null,
        ComparisonSide side = ComparisonSide.Left)
    {
        var result = new Dictionary<HashAlgorithmType, string>();

        foreach (var algorithm in algorithms)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result[algorithm] = ComputeHash(file, algorithm, progressSink, side);
        }

        return result;
    }

    private string ComputeHash(
        IFileEntry file,
        HashAlgorithmType algorithm,
        IComparisonProgressSink? progressSink,
        ComparisonSide side)
    {
        return algorithm switch
        {
            HashAlgorithmType.Crc32 => ComputeUsingIncremental(file, () => new Crc32(), progressSink, side),
            HashAlgorithmType.Md5 => ComputeUsingHashAlgorithm(file, MD5.Create, progressSink, side),
            HashAlgorithmType.Sha256 => ComputeUsingHashAlgorithm(file, SHA256.Create, progressSink, side),
            HashAlgorithmType.XxHash64 => ComputeUsingIncremental(file, () => new XxHash64(), progressSink, side),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, null)
        };
    }

    private static string ComputeUsingHashAlgorithm(
        IFileEntry file,
        Func<HashAlgorithm> factory,
        IComparisonProgressSink? progressSink,
        ComparisonSide side)
    {
        using var algorithm = factory();
        using var stream = file.OpenRead();
        var buffer = ArrayPool<byte>.Shared.Rent(8192);
        try
        {
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                algorithm.TransformBlock(buffer, 0, read, null, 0);
                progressSink?.BytesRead(side, read);
            }

            algorithm.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return Convert.ToHexString(algorithm.Hash!).ToLowerInvariant();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static string ComputeUsingIncremental(
        IFileEntry file,
        Func<NonCryptographicHashAlgorithm> factory,
        IComparisonProgressSink? progressSink,
        ComparisonSide side)
    {
        var algorithm = factory();
        try
        {
            using var stream = file.OpenRead();
            var buffer = ArrayPool<byte>.Shared.Rent(8192);
            try
            {
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    algorithm.Append(buffer.AsSpan(0, read));
                    progressSink?.BytesRead(side, read);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
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
