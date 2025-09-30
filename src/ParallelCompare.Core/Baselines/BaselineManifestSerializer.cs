using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using ParallelCompare.Core.Options;

namespace ParallelCompare.Core.Baselines;

/// <summary>
/// Handles reading and writing baseline manifest files.
/// </summary>
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

    /// <summary>
    /// Writes the manifest to disk at the specified path.
    /// </summary>
    /// <param name="manifest">Manifest to persist.</param>
    /// <param name="path">Destination path for the manifest.</param>
    /// <param name="cancellationToken">Token used to cancel the write operation.</param>
    public async Task WriteAsync(BaselineManifest manifest, string path, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? Directory.GetCurrentDirectory());
        await using var stream = File.Create(path);
        var payload = ToSerializable(manifest);
        await JsonSerializer.SerializeAsync(stream, payload, SerializerOptions, cancellationToken);
    }

    /// <summary>
    /// Reads a manifest from disk.
    /// </summary>
    /// <param name="path">Manifest file path.</param>
    /// <param name="cancellationToken">Token used to cancel the read operation.</param>
    /// <returns>The deserialized manifest.</returns>
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

    /// <summary>
    /// Provides ordinal equality for <see cref="HashAlgorithmType"/> values.
    /// </summary>
    private sealed class HashAlgorithmComparer : IEqualityComparer<HashAlgorithmType>
    {
        /// <summary>
        /// Gets the singleton comparer instance.
        /// </summary>
        public static readonly HashAlgorithmComparer Instance = new();

        /// <inheritdoc />
        public bool Equals(HashAlgorithmType x, HashAlgorithmType y) => x == y;

        /// <inheritdoc />
        public int GetHashCode(HashAlgorithmType obj) => (int)obj;
    }

    /// <summary>
    /// Serializable representation of a <see cref="BaselineManifest"/>.
    /// </summary>
    private sealed record SerializableBaselineManifest
    {
        /// <summary>
        /// Gets or sets the manifest version.
        /// </summary>
        public string Version { get; init; } = BaselineManifest.CurrentVersion;

        /// <summary>
        /// Gets or sets the source directory path captured in the manifest.
        /// </summary>
        public string SourcePath { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the capture timestamp.
        /// </summary>
        public DateTimeOffset CreatedAt { get; init; }

        /// <summary>
        /// Gets or sets the ignore patterns applied during capture.
        /// </summary>
        public string[]? IgnorePatterns { get; init; }

        /// <summary>
        /// Gets or sets a value indicating whether comparisons were case sensitive.
        /// </summary>
        public bool CaseSensitive { get; init; }

        /// <summary>
        /// Gets or sets the modified time tolerance.
        /// </summary>
        public TimeSpan? ModifiedTimeTolerance { get; init; }

        /// <summary>
        /// Gets or sets the hash algorithms captured with the manifest.
        /// </summary>
        public string[]? Algorithms { get; init; }

        /// <summary>
        /// Gets or sets the root entry describing the directory tree.
        /// </summary>
        public SerializableBaselineEntry? Root { get; init; }
    }

    /// <summary>
    /// Serializable representation of a <see cref="BaselineEntry"/>.
    /// </summary>
    private sealed record SerializableBaselineEntry
    {
        /// <summary>
        /// Gets or sets the entry name.
        /// </summary>
        public string? Name { get; init; }

        /// <summary>
        /// Gets or sets the path relative to the manifest root.
        /// </summary>
        public string? RelativePath { get; init; }

        /// <summary>
        /// Gets or sets the entry type.
        /// </summary>
        public BaselineEntryType EntryType { get; init; }

        /// <summary>
        /// Gets or sets the file size, when applicable.
        /// </summary>
        public long? Size { get; init; }

        /// <summary>
        /// Gets or sets the last modified timestamp.
        /// </summary>
        public DateTimeOffset? Modified { get; init; }

        /// <summary>
        /// Gets or sets the stored hash values.
        /// </summary>
        public Dictionary<string, string>? Hashes { get; init; }

        /// <summary>
        /// Gets or sets the child entries for directories.
        /// </summary>
        public SerializableBaselineEntry[]? Children { get; init; }
    }
}
