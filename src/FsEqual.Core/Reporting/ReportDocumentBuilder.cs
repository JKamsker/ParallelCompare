using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using FsEqual.Core.Comparison;
using FsEqual.Core.Options;

namespace FsEqual.Core.Reporting;

/// <summary>
/// Creates standardized export documents from comparison results.
/// </summary>
public sealed class ReportDocumentBuilder
{
    private readonly Func<DateTimeOffset> _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportDocumentBuilder"/> class.
    /// </summary>
    /// <param name="clock">Clock used to capture the generation timestamp.</param>
    public ReportDocumentBuilder(Func<DateTimeOffset>? clock = null)
    {
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Builds a structured export document from the comparison result.
    /// </summary>
    /// <param name="result">Comparison result to normalize.</param>
    /// <param name="settings">Resolved settings used for the run.</param>
    /// <returns>The structured export document.</returns>
    public ReportDocument Build(ComparisonResult result, ResolvedCompareSettings settings)
    {
        var metadata = CreateMetadata(result, settings);
        var differences = FlattenDifferences(result.Root);
        return new ReportDocument(metadata, result.Summary, differences);
    }

    private ReportMetadata CreateMetadata(ComparisonResult result, ResolvedCompareSettings settings)
    {
        var algorithms = settings.Algorithms.IsDefaultOrEmpty
            ? ImmutableArray<string>.Empty
            : settings.Algorithms.Select(static algorithm => algorithm.ToString()).ToImmutableArray();

        var primary = algorithms.IsDefaultOrEmpty || algorithms.Length == 0 ? null : algorithms[0];

        var baseline = result.Baseline is null
            ? null
            : new ReportBaselineMetadata(
                result.Baseline.ManifestPath,
                result.Baseline.SourcePath,
                result.Baseline.CreatedAt,
                result.Baseline.Algorithms.IsDefaultOrEmpty
                    ? ImmutableArray<string>.Empty
                    : result.Baseline.Algorithms.Select(static algorithm => algorithm.ToString()).ToImmutableArray());

        return new ReportMetadata(
            _clock(),
            result.LeftPath,
            result.RightPath,
            settings.Mode.ToString(),
            algorithms,
            primary,
            settings.UsesBaseline,
            baseline);
    }

    private static ImmutableArray<ReportDifference> FlattenDifferences(ComparisonNode root)
    {
        var builder = ImmutableArray.CreateBuilder<ReportDifference>();
        AppendNode(builder, root);
        return builder.ToImmutable();
    }

    private static void AppendNode(ImmutableArray<ReportDifference>.Builder builder, ComparisonNode node)
    {
        if (node.Status != ComparisonStatus.Equal || node.HasDifferences)
        {
            builder.Add(new ReportDifference(
                node.Name,
                ReportTextUtilities.GetNodePath(node),
                node.NodeType.ToString(),
                node.Status.ToString(),
                CreateDetail(node.Detail)));
        }

        foreach (var child in node.Children)
        {
            AppendNode(builder, child);
        }
    }

    private static ReportFileDetail? CreateDetail(FileComparisonDetail? detail)
    {
        if (detail is null)
        {
            return null;
        }

        return new ReportFileDetail(
            detail.LeftSize,
            detail.RightSize,
            detail.LeftModified,
            detail.RightModified,
            ToDictionary(detail.LeftHashes),
            ToDictionary(detail.RightHashes),
            detail.ErrorMessage);
    }

    private static IReadOnlyDictionary<string, string>? ToDictionary(IReadOnlyDictionary<HashAlgorithmType, string>? hashes)
    {
        if (hashes is null || hashes.Count == 0)
        {
            return null;
        }

        var dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in hashes)
        {
            dictionary[pair.Key.ToString()] = pair.Value;
        }

        return dictionary;
    }
}
