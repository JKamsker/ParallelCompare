using System.Text.Json;
using System.Text.Json.Serialization;

namespace FsEqual.Tool.Comparison;

internal sealed class BaselineSnapshot
{
    private BaselineSnapshot(IReadOnlyDictionary<string, BaselineFile> files, string hashAlgorithm)
    {
        Files = files;
        HashAlgorithm = hashAlgorithm;
    }

    public IReadOnlyDictionary<string, BaselineFile> Files { get; }
    public string HashAlgorithm { get; }

    public static BaselineSnapshot Load(string path)
    {
        using var stream = File.OpenRead(path);
        var model = JsonSerializer.Deserialize(stream, SnapshotJsonContext.Default.SnapshotModel)
                    ?? throw new InvalidOperationException("Snapshot file is empty or invalid.");

        var comparer = model.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        var files = model.Files.ToDictionary(x => x.Path, x => new BaselineFile(x.Path, x.Size, x.LastWriteTimeUtc, x.Hash, x.HashAlgorithm), comparer);
        return new BaselineSnapshot(files, model.HashAlgorithm ?? "unknown");
    }

    public SnapshotModel ToModel(bool caseSensitive)
    {
        return new SnapshotModel
        {
            CaseSensitive = caseSensitive,
            HashAlgorithm = HashAlgorithm,
            Files = Files.Values.Select(file => new SnapshotFile
            {
                Path = file.Path,
                Size = file.Size,
                LastWriteTimeUtc = file.LastWriteTimeUtc,
                Hash = file.Hash,
                HashAlgorithm = file.HashAlgorithm
            }).ToList()
        };
    }
}

internal sealed record BaselineFile(string Path, long Size, DateTime LastWriteTimeUtc, string? Hash, string? HashAlgorithm);

internal sealed record SnapshotModel
{
    public List<SnapshotFile> Files { get; init; } = new();
    public bool CaseSensitive { get; init; }
    public string? HashAlgorithm { get; init; }
}

internal sealed record SnapshotFile
{
    public required string Path { get; init; }
    public required long Size { get; init; }
    public required DateTime LastWriteTimeUtc { get; init; }
    public string? Hash { get; init; }
    public string? HashAlgorithm { get; init; }
}

[JsonSerializable(typeof(SnapshotModel))]
internal partial class SnapshotJsonContext : JsonSerializerContext;
