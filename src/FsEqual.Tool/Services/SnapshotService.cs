using System.Text.Json;
using System.Text.Json.Serialization;
using FsEqual.Tool.Models;

namespace FsEqual.Tool.Services;

public sealed class SnapshotService
{
    private readonly DirectoryEnumerator _enumerator = new();

    public async Task<DirectorySnapshot> CaptureAsync(
        string root,
        ComparisonOptions options,
        HashAlgorithmKind algorithm,
        CancellationToken cancellationToken)
    {
        var snapshot = await _enumerator.CaptureAsync(root, options, cancellationToken);
        await DirectoryComparator.ComputeHashesAsync(
            snapshot,
            algorithm,
            options,
            cancellationToken,
            new System.Collections.Concurrent.ConcurrentBag<ComparisonError>(),
            null);
        return snapshot;
    }

    public async Task SaveAsync(
        DirectorySnapshot snapshot,
        HashAlgorithmKind algorithm,
        string outputPath,
        bool caseSensitive,
        CancellationToken cancellationToken)
    {
        var model = new SnapshotDocument
        {
            Version = "1.0",
            Root = snapshot.Root,
            CaseSensitive = caseSensitive,
            Algorithm = algorithm.ToString().ToLowerInvariant(),
            Files = snapshot.Files.Values
                .Select(file => new SnapshotEntry
                {
                    Path = file.RelativePath,
                    Size = file.Size,
                    LastWriteTimeUtc = file.LastWriteTimeUtc,
                    Hash = file.Hash,
                    HashAlgorithm = file.HashAlgorithm?.ToString().ToLowerInvariant(),
                })
                .ToList(),
            Directories = snapshot.Directories.ToList(),
        };

        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, model, JsonOptions, cancellationToken);
    }

    public async Task<DirectorySnapshot> LoadAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var document = await JsonSerializer.DeserializeAsync<SnapshotDocument>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException($"Invalid snapshot file '{path}'.");

        var snapshot = new DirectorySnapshot(document.Root, document.CaseSensitive);
        foreach (var dir in document.Directories)
        {
            snapshot.Directories.Add(dir);
        }

        foreach (var file in document.Files)
        {
            var metadata = new FileMetadata(file.Path, file.Size, file.LastWriteTimeUtc);
            if (!string.IsNullOrEmpty(file.Hash) && Enum.TryParse<HashAlgorithmKind>(file.HashAlgorithm, true, out var algo))
            {
                metadata.SetHash(file.Hash!, algo);
            }

            snapshot.Files[file.Path] = metadata;
        }

        return snapshot;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private sealed class SnapshotDocument
    {
        public string Version { get; set; } = "1.0";
        public string Root { get; set; } = string.Empty;
        public bool CaseSensitive { get; set; }
        public string Algorithm { get; set; } = "crc32";
        public List<string> Directories { get; set; } = new();
        public List<SnapshotEntry> Files { get; set; } = new();
    }

    private sealed class SnapshotEntry
    {
        public string Path { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTimeOffset LastWriteTimeUtc { get; set; }
        public string? Hash { get; set; }
        public string? HashAlgorithm { get; set; }
    }
}
