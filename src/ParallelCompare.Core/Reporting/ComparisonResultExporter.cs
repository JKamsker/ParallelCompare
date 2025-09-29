using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using ParallelCompare.Core.Comparison;

namespace ParallelCompare.Core.Reporting;

public sealed class ComparisonResultExporter
{
    private readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task WriteJsonAsync(ComparisonResult result, string path, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? Directory.GetCurrentDirectory());
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, ToSerializable(result), _options, cancellationToken);
    }

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

    private sealed record SerializableComparisonResult
    {
        public required string LeftPath { get; init; }
        public required string RightPath { get; init; }
        public required SerializableComparisonNode Root { get; init; }
        public required ComparisonSummary Summary { get; init; }
        public SerializableBaselineMetadata? Baseline { get; init; }
    }

    private sealed record SerializableComparisonNode
    {
        public required string Name { get; init; }
        public required string RelativePath { get; init; }
        public required string NodeType { get; init; }
        public required string Status { get; init; }
        public SerializableFileDetail? Detail { get; init; }
        public required List<SerializableComparisonNode> Children { get; init; }
    }

    private sealed record SerializableFileDetail
    {
        public long? LeftSize { get; init; }
        public long? RightSize { get; init; }
        public DateTimeOffset? LeftModified { get; init; }
        public DateTimeOffset? RightModified { get; init; }
        public Dictionary<string, string>? LeftHashes { get; init; }
        public Dictionary<string, string>? RightHashes { get; init; }
        public string? ErrorMessage { get; init; }
    }

    private sealed record SerializableBaselineMetadata
    {
        public required string ManifestPath { get; init; }
        public required string SourcePath { get; init; }
        public required DateTimeOffset CreatedAt { get; init; }
        public string[]? Algorithms { get; init; }
    }
}
