using System.Security.Cryptography;
using Standart.Hash.xxHash;

namespace FsEqual.Tool.Comparison;

internal static class Hashing
{
    public static string ComputeHash(string path, HashAlgorithmKind algorithm, CancellationToken cancellationToken)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return ComputeHash(stream, algorithm, cancellationToken);
    }

    public static string ComputeHash(Stream stream, HashAlgorithmKind algorithm, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return algorithm switch
        {
            HashAlgorithmKind.Crc32 => Crc32.Compute(stream),
            HashAlgorithmKind.Md5 => Compute(stream, MD5.Create),
            HashAlgorithmKind.Sha256 => Compute(stream, SHA256.Create),
            HashAlgorithmKind.Xxh64 => ComputeXxh64(stream),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, null)
        };
    }

    private static string Compute(Stream stream, Func<HashAlgorithm> factory)
    {
        using var algorithm = factory();
        stream.Position = 0;
        var hash = algorithm.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }

    private static string ComputeXxh64(Stream stream)
    {
        stream.Position = 0;
        var hash = xxHash64.ComputeHash(stream);
        Span<byte> buffer = stackalloc byte[sizeof(ulong)];
        BitConverter.TryWriteBytes(buffer, hash);
        return Convert.ToHexString(buffer);
    }
}

internal static class Crc32
{
    private static readonly uint[] Table = CreateTable();

    public static string Compute(Stream stream)
    {
        stream.Position = 0;
        unchecked
        {
            uint crc = 0xFFFFFFFF;
            Span<byte> buffer = stackalloc byte[8192];
            int read;
            while ((read = stream.Read(buffer)) > 0)
            {
                for (var i = 0; i < read; i++)
                {
                    var index = (byte)((crc ^ buffer[i]) & 0xFF);
                    crc = Table[index] ^ (crc >> 8);
                }
            }

            crc ^= 0xFFFFFFFF;
            return crc.ToString("X8");
        }
    }

    private static uint[] CreateTable()
    {
        const uint polynomial = 0xEDB88320u;
        var table = new uint[256];
        for (uint i = 0; i < table.Length; i++)
        {
            uint entry = i;
            for (var j = 0; j < 8; j++)
            {
                if ((entry & 1) == 1)
                {
                    entry = (entry >> 1) ^ polynomial;
                }
                else
                {
                    entry >>= 1;
                }
            }

            table[i] = entry;
        }

        return table;
    }
}
