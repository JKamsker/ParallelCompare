using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FsEqual.Core.Comparison;
using FsEqual.Core.Options;
using FsEqual.Core.Reporting;

namespace FsEqual.App.Interactive;

/// <summary>
/// Produces filtered exports from the interactive comparison session.
/// </summary>
public sealed class InteractiveExportService
{
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    /// <summary>
    /// Exports the specified nodes to the requested format.
    /// </summary>
    /// <param name="result">Comparison result providing context.</param>
    /// <param name="nodes">Nodes selected for export.</param>
    /// <param name="format">Export format (json, csv, markdown).</param>
    /// <param name="destination">Destination file path.</param>
    /// <param name="activeAlgorithm">Active hash algorithm for CSV/Markdown columns.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    public async Task ExportAsync(
        ComparisonResult result,
        IReadOnlyList<ComparisonNode> nodes,
        string format,
        string destination,
        HashAlgorithmType? activeAlgorithm,
        CancellationToken cancellationToken)
    {
        if (nodes.Count == 0)
        {
            throw new InvalidOperationException("There are no nodes to export for the current filter.");
        }

        var fullPath = Path.GetFullPath(destination);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory());

        switch (format.Trim().ToLowerInvariant())
        {
            case "json":
                await ExportJsonAsync(result, nodes, fullPath, cancellationToken);
                break;
            case "csv":
                await ExportCsvAsync(result, nodes, fullPath, activeAlgorithm, cancellationToken);
                break;
            case "markdown":
            case "md":
                await ExportMarkdownAsync(result, nodes, fullPath, activeAlgorithm, cancellationToken);
                break;
            default:
                throw new InvalidOperationException($"Unsupported export format '{format}'.");
        }
    }

    private async Task ExportJsonAsync(
        ComparisonResult result,
        IReadOnlyList<ComparisonNode> nodes,
        string destination,
        CancellationToken cancellationToken)
    {
        await using var stream = File.Create(destination);
        var payload = new
        {
            generatedAt = DateTimeOffset.UtcNow,
            result.LeftPath,
            result.RightPath,
            nodes = nodes.Select(node => new
            {
                path = ReportTextUtilities.GetNodePath(node),
                type = node.NodeType.ToString(),
                status = node.Status.ToString(),
                detail = node.Detail is null
                    ? null
                    : new
                    {
                        node.Detail.LeftSize,
                        node.Detail.RightSize,
                        node.Detail.LeftModified,
                        node.Detail.RightModified,
                        LeftHashes = node.Detail.LeftHashes?.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
                        RightHashes = node.Detail.RightHashes?.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
                        node.Detail.ErrorMessage
                    }
            }).ToList()
        };

        await JsonSerializer.SerializeAsync(stream, payload, _jsonOptions, cancellationToken);
    }

    private async Task ExportCsvAsync(
        ComparisonResult result,
        IReadOnlyList<ComparisonNode> nodes,
        string destination,
        HashAlgorithmType? activeAlgorithm,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Path,Type,Status,LeftSize,RightSize,LeftModified,RightModified,LeftHash,RightHash,Error");

        foreach (var node in nodes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var detail = node.Detail;
            var leftHash = detail is null ? string.Empty : GetHash(detail.LeftHashes, activeAlgorithm);
            var rightHash = detail is null ? string.Empty : GetHash(detail.RightHashes, activeAlgorithm);
            var error = detail?.ErrorMessage ?? string.Empty;

            builder.AppendLine(string.Join(',', new[]
            {
                ReportTextUtilities.EscapeCsv(ReportTextUtilities.GetNodePath(node)),
                ReportTextUtilities.EscapeCsv(node.NodeType.ToString()),
                ReportTextUtilities.EscapeCsv(node.Status.ToString()),
                ReportTextUtilities.EscapeCsv(detail?.LeftSize?.ToString() ?? string.Empty),
                ReportTextUtilities.EscapeCsv(detail?.RightSize?.ToString() ?? string.Empty),
                ReportTextUtilities.EscapeCsv(detail?.LeftModified?.ToString("u") ?? string.Empty),
                ReportTextUtilities.EscapeCsv(detail?.RightModified?.ToString("u") ?? string.Empty),
                ReportTextUtilities.EscapeCsv(leftHash ?? string.Empty),
                ReportTextUtilities.EscapeCsv(rightHash ?? string.Empty),
                ReportTextUtilities.EscapeCsv(error)
            }));
        }

        await File.WriteAllTextAsync(destination, builder.ToString(), cancellationToken);
    }

    private async Task ExportMarkdownAsync(
        ComparisonResult result,
        IReadOnlyList<ComparisonNode> nodes,
        string destination,
        HashAlgorithmType? activeAlgorithm,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# FsEqual Export");
        builder.AppendLine();
        builder.AppendLine($"- Left: `{ReportTextUtilities.EscapeMarkdown(result.LeftPath)}`");
        builder.AppendLine($"- Right: `{ReportTextUtilities.EscapeMarkdown(result.RightPath)}`");
        builder.AppendLine($"- Generated: {DateTimeOffset.UtcNow:u}");
        builder.AppendLine();
        builder.AppendLine("| Path | Type | Status | Left Size | Right Size | Left Modified | Right Modified | Left Hash | Right Hash | Error |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |");

        foreach (var node in nodes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var detail = node.Detail;
            var leftHash = detail is null ? string.Empty : GetHash(detail.LeftHashes, activeAlgorithm) ?? string.Empty;
            var rightHash = detail is null ? string.Empty : GetHash(detail.RightHashes, activeAlgorithm) ?? string.Empty;

            builder.AppendLine(string.Join(" | ", new[]
            {
                ReportTextUtilities.EscapeMarkdown(ReportTextUtilities.GetNodePath(node)),
                ReportTextUtilities.EscapeMarkdown(node.NodeType.ToString()),
                ReportTextUtilities.EscapeMarkdown(node.Status.ToString()),
                ReportTextUtilities.EscapeMarkdown(detail?.LeftSize?.ToString() ?? string.Empty),
                ReportTextUtilities.EscapeMarkdown(detail?.RightSize?.ToString() ?? string.Empty),
                ReportTextUtilities.EscapeMarkdown(detail?.LeftModified?.ToString("u") ?? string.Empty),
                ReportTextUtilities.EscapeMarkdown(detail?.RightModified?.ToString("u") ?? string.Empty),
                ReportTextUtilities.EscapeMarkdown(leftHash),
                ReportTextUtilities.EscapeMarkdown(rightHash),
                ReportTextUtilities.EscapeMarkdown(detail?.ErrorMessage ?? string.Empty)
            }));
        }

        await File.WriteAllTextAsync(destination, builder.ToString(), cancellationToken);
    }

    private static string? GetHash(
        IReadOnlyDictionary<HashAlgorithmType, string>? hashes,
        HashAlgorithmType? preferred)
    {
        if (hashes is null || hashes.Count == 0)
        {
            return null;
        }

        if (preferred is not null && hashes.TryGetValue(preferred.Value, out var value))
        {
            return value;
        }

        return hashes.OrderBy(pair => pair.Key.ToString(), StringComparer.OrdinalIgnoreCase)
            .Select(pair => pair.Value)
            .FirstOrDefault();
    }
}
