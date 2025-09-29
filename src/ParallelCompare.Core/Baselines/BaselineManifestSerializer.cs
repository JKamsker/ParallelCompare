using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using ParallelCompare.Core.Options;

namespace ParallelCompare.Core.Baselines;

public sealed class BaselineManifestSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    public async Task WriteAsync(BaselineManifest manifest, string path, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? Directory.GetCurrentDirectory());
        await using var stream = File.Create(path);
        var payload = ToSerializable(manifest);
        await JsonSerializer.SerializeAsync(stream, payload, SerializerOptions, cancellationToken);
    }

    public async Task<BaselineManifest> ReadAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var payload = await JsonSerializer.DeserializeAsync<SerializableBaselineManifest>(stream, SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("Baseline manifest could not be deserialized.");

        return FromSerializable(payload);
    }

    private static SerializableBaselineManifest ToSerializable(BaselineManifest manifest)
    {
        return new SerializableBaselineManifest
        {
            Version = manifest.Version,
            SourcePath = manifest.SourcePath,
            CreatedAt = manifest.CreatedAt,
            IgnorePatterns = manifest.IgnorePatterns.ToArray(),
            CaseSensitive = manifest.CaseSensitive,
            ModifiedTimeTolerance = manifest.ModifiedTimeTolerance,
            Algorithms = manifest.Algorithms.Select(a => a.ToString()).ToArray(),
            Root = ToSerializable(manifest.Root)
        };
    }

    private static BaselineManifest FromSerializable(SerializableBaselineManifest manifest)
    {
        if (!string.Equals(manifest.Version, BaselineManifest.CurrentVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unsupported baseline manifest version '{manifest.Version}'.");
        }

        return new BaselineManifest(
            manifest.Version,
            manifest.SourcePath,
            manifest.CreatedAt,
            manifest.IgnorePatterns?.ToImmutableArray() ?? ImmutableArray<string>.Empty,
            manifest.CaseSensitive,
            manifest.ModifiedTimeTolerance,
            manifest.Algorithms is null
                ? ImmutableArray<HashAlgorithmType>.Empty
                : manifest.Algorithms.Select(ParseAlgorithm).ToImmutableArray(),
            FromSerializable(manifest.Root ?? throw new InvalidOperationException("Baseline manifest root is missing."))
        );
    }

    private static SerializableBaselineEntry ToSerializable(BaselineEntry entry)
    {
        return new SerializableBaselineEntry
        {
            Name = entry.Name,
            RelativePath = entry.RelativePath,
            EntryType = entry.EntryType,
            Size = entry.Size,
            Modified = entry.Modified,
            Hashes = entry.Hashes.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value, StringComparer.OrdinalIgnoreCase),
            Children = entry.Children.Select(ToSerializable).ToArray()
        };
    }

    private static BaselineEntry FromSerializable(SerializableBaselineEntry entry)
    {
        var hashes = entry.Hashes is null
            ? ImmutableDictionary<HashAlgorithmType, string>.Empty
            : entry.Hashes
                .ToDictionary(kvp => ParseAlgorithm(kvp.Key), kvp => kvp.Value, HashAlgorithmComparer.Instance)
                .ToImmutableDictionary(HashAlgorithmComparer.Instance);

        return new BaselineEntry(
            entry.Name ?? throw new InvalidOperationException("Baseline entry name is required."),
            entry.RelativePath ?? string.Empty,
            entry.EntryType,
            entry.Size,
            entry.Modified,
            hashes,
            entry.Children?.Select(FromSerializable).ToImmutableArray() ?? ImmutableArray<BaselineEntry>.Empty);
    }

    private static HashAlgorithmType ParseAlgorithm(string value)
    {
        return Enum.TryParse<HashAlgorithmType>(value, true, out var result)
            ? result
            : throw new InvalidOperationException($"Unsupported hash algorithm '{value}' in baseline manifest.");
    }

    private sealed class HashAlgorithmComparer : IEqualityComparer<HashAlgorithmType>
    {
        public static readonly HashAlgorithmComparer Instance = new();

        public bool Equals(HashAlgorithmType x, HashAlgorithmType y) => x == y;

        public int GetHashCode(HashAlgorithmType obj) => (int)obj;
    }

    private sealed record SerializableBaselineManifest
    {
        public string Version { get; init; } = BaselineManifest.CurrentVersion;
        public string SourcePath { get; init; } = string.Empty;
        public DateTimeOffset CreatedAt { get; init; }
        public string[]? IgnorePatterns { get; init; }
        public bool CaseSensitive { get; init; }
        public TimeSpan? ModifiedTimeTolerance { get; init; }
        public string[]? Algorithms { get; init; }
        public SerializableBaselineEntry? Root { get; init; }
    }

    private sealed record SerializableBaselineEntry
    {
        public string? Name { get; init; }
        public string? RelativePath { get; init; }
        public BaselineEntryType EntryType { get; init; }
        public long? Size { get; init; }
        public DateTimeOffset? Modified { get; init; }
        public Dictionary<string, string>? Hashes { get; init; }
        public SerializableBaselineEntry[]? Children { get; init; }
    }
}
