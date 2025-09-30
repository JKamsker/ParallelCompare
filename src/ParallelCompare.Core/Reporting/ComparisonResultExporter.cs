using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using ParallelCompare.Core.Comparison;

namespace ParallelCompare.Core.Reporting;

/// <summary>
/// Writes comparison results to structured JSON outputs.
/// </summary>
public sealed class ComparisonResultExporter
{
    private readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    /// <summary>
    /// Writes the full comparison result to a JSON file.
    /// </summary>
    /// <param name="result">Comparison result to export.</param>
    /// <param name="path">Destination path for the JSON file.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    public async Task WriteJsonAsync(ComparisonResult result, string path, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? Directory.GetCurrentDirectory());
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, ToSerializable(result), _options, cancellationToken);
    }

    /// <summary>
    /// Writes only the summary portion of the comparison result to JSON.
    /// </summary>
    /// <param name="result">Comparison result containing the summary.</param>
    /// <param name="path">Destination path for the JSON file.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    public async Task WriteSummaryAsync(ComparisonResult result, string path, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? Directory.GetCurrentDirectory());
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, result.Summary, _options, cancellationToken);
    }

    private static SerializableComparisonResult ToSerializable(ComparisonResult result)
    {
        return new SerializableComparisonResult
        {
            LeftPath = result.LeftPath,
            RightPath = result.RightPath,
            Root = ToSerializable(result.Root),
            Summary = result.Summary,
            Baseline = result.Baseline is null ? null : new SerializableBaselineMetadata
            {
                ManifestPath = result.Baseline.ManifestPath,
                SourcePath = result.Baseline.SourcePath,
                CreatedAt = result.Baseline.CreatedAt,
                Algorithms = result.Baseline.Algorithms.Select(a => a.ToString()).ToArray()
            }
        };
    }

    private static SerializableComparisonNode ToSerializable(ComparisonNode node)
    {
        return new SerializableComparisonNode
        {
            Name = node.Name,
            RelativePath = node.RelativePath,
            NodeType = node.NodeType.ToString(),
            Status = node.Status.ToString(),
            Detail = node.Detail is null ? null : new SerializableFileDetail
            {
                LeftSize = node.Detail.LeftSize,
                RightSize = node.Detail.RightSize,
                LeftModified = node.Detail.LeftModified,
                RightModified = node.Detail.RightModified,
                LeftHashes = node.Detail.LeftHashes?.ToDictionary(x => x.Key.ToString(), x => x.Value),
                RightHashes = node.Detail.RightHashes?.ToDictionary(x => x.Key.ToString(), x => x.Value),
                ErrorMessage = node.Detail.ErrorMessage
            },
            Children = node.Children.Select(ToSerializable).ToList()
        };
    }

    /// <summary>
    /// Serializable container for comparison results written to disk.
    /// </summary>
    private sealed record SerializableComparisonResult
    {
        /// <summary>
        /// Gets or sets the left input path.
        /// </summary>
        public required string LeftPath { get; init; }

        /// <summary>
        /// Gets or sets the right input path.
        /// </summary>
        public required string RightPath { get; init; }

        /// <summary>
        /// Gets or sets the serialized root node.
        /// </summary>
        public required SerializableComparisonNode Root { get; init; }

        /// <summary>
        /// Gets or sets the comparison summary.
        /// </summary>
        public required ComparisonSummary Summary { get; init; }

        /// <summary>
        /// Gets or sets the baseline metadata when present.
        /// </summary>
        public SerializableBaselineMetadata? Baseline { get; init; }
    }

    /// <summary>
    /// Serializable representation of a <see cref="ComparisonNode"/>.
    /// </summary>
    private sealed record SerializableComparisonNode
    {
        /// <summary>
        /// Gets or sets the entry name.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Gets or sets the relative path for the entry.
        /// </summary>
        public required string RelativePath { get; init; }

        /// <summary>
        /// Gets or sets the node type.
        /// </summary>
        public required string NodeType { get; init; }

        /// <summary>
        /// Gets or sets the status string.
        /// </summary>
        public required string Status { get; init; }

        /// <summary>
        /// Gets or sets the serialized file detail.
        /// </summary>
        public SerializableFileDetail? Detail { get; init; }

        /// <summary>
        /// Gets or sets the child nodes.
        /// </summary>
        public required List<SerializableComparisonNode> Children { get; init; }
    }

    /// <summary>
    /// Serializable representation of <see cref="FileComparisonDetail"/>.
    /// </summary>
    private sealed record SerializableFileDetail
    {
        /// <summary>
        /// Gets or sets the left file size.
        /// </summary>
        public long? LeftSize { get; init; }

        /// <summary>
        /// Gets or sets the right file size.
        /// </summary>
        public long? RightSize { get; init; }

        /// <summary>
        /// Gets or sets the left file modified timestamp.
        /// </summary>
        public DateTimeOffset? LeftModified { get; init; }

        /// <summary>
        /// Gets or sets the right file modified timestamp.
        /// </summary>
        public DateTimeOffset? RightModified { get; init; }

        /// <summary>
        /// Gets or sets the left file hashes.
        /// </summary>
        public Dictionary<string, string>? LeftHashes { get; init; }

        /// <summary>
        /// Gets or sets the right file hashes.
        /// </summary>
        public Dictionary<string, string>? RightHashes { get; init; }

        /// <summary>
        /// Gets or sets the error message when the file could not be compared.
        /// </summary>
        public string? ErrorMessage { get; init; }
    }

    /// <summary>
    /// Serializable representation of <see cref="BaselineMetadata"/>.
    /// </summary>
    private sealed record SerializableBaselineMetadata
    {
        /// <summary>
        /// Gets or sets the manifest path.
        /// </summary>
        public required string ManifestPath { get; init; }

        /// <summary>
        /// Gets or sets the original source path captured in the baseline.
        /// </summary>
        public required string SourcePath { get; init; }

        /// <summary>
        /// Gets or sets the capture timestamp.
        /// </summary>
        public required DateTimeOffset CreatedAt { get; init; }

        /// <summary>
        /// Gets or sets the algorithms recorded with the baseline.
        /// </summary>
        public string[]? Algorithms { get; init; }
    }
}
