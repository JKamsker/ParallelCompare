using System.Collections.Generic;
using System.Collections.Immutable;
using FsEqual.Core.Comparison;

namespace FsEqual.Core.Reporting;

/// <summary>
/// Represents the standardized document structure shared across all exports.
/// </summary>
/// <param name="Metadata">Metadata describing the comparison run.</param>
/// <param name="Summary">File-level summary metrics.</param>
/// <param name="Differences">Flattened list of nodes that differ between trees.</param>
public sealed record ReportDocument(
    ReportMetadata Metadata,
    ComparisonSummary Summary,
    ImmutableArray<ReportDifference> Differences
);

/// <summary>
/// Provides contextual metadata for an export document.
/// </summary>
/// <param name="GeneratedAt">UTC timestamp when the export was generated.</param>
/// <param name="LeftPath">Absolute path to the left input.</param>
/// <param name="RightPath">Absolute path to the right input.</param>
/// <param name="Mode">Comparison mode executed for the run.</param>
/// <param name="Algorithms">Algorithms used to compute hashes.</param>
/// <param name="PrimaryAlgorithm">Preferred algorithm for human-readable exports.</param>
/// <param name="UsesBaseline">Indicates whether the comparison used a baseline manifest.</param>
/// <param name="Baseline">Baseline metadata when available.</param>
public sealed record ReportMetadata(
    DateTimeOffset GeneratedAt,
    string LeftPath,
    string RightPath,
    string Mode,
    ImmutableArray<string> Algorithms,
    string? PrimaryAlgorithm,
    bool UsesBaseline,
    ReportBaselineMetadata? Baseline
);

/// <summary>
/// Describes baseline context attached to a report document.
/// </summary>
/// <param name="ManifestPath">Path to the baseline manifest file.</param>
/// <param name="SourcePath">Original source path captured by the baseline.</param>
/// <param name="CreatedAt">Timestamp when the baseline was created.</param>
/// <param name="Algorithms">Algorithms captured within the baseline.</param>
public sealed record ReportBaselineMetadata(
    string ManifestPath,
    string SourcePath,
    DateTimeOffset CreatedAt,
    ImmutableArray<string> Algorithms
);

/// <summary>
/// Represents a flattened difference entry exported by structured formats.
/// </summary>
/// <param name="Name">Node display name.</param>
/// <param name="Path">Normalized relative path from the comparison root.</param>
/// <param name="Type">Node type (file or directory).</param>
/// <param name="Status">Comparison status for the node.</param>
/// <param name="Detail">Optional file detail metadata.</param>
public sealed record ReportDifference(
    string Name,
    string Path,
    string Type,
    string Status,
    ReportFileDetail? Detail
);

/// <summary>
/// Captures file-specific metadata for difference entries.
/// </summary>
/// <param name="LeftSize">Left file size in bytes.</param>
/// <param name="RightSize">Right file size in bytes.</param>
/// <param name="LeftModified">Last modified timestamp for the left file.</param>
/// <param name="RightModified">Last modified timestamp for the right file.</param>
/// <param name="LeftHashes">Hashes calculated for the left file.</param>
/// <param name="RightHashes">Hashes calculated for the right file.</param>
/// <param name="ErrorMessage">Error message when the node encountered a failure.</param>
public sealed record ReportFileDetail(
    long? LeftSize,
    long? RightSize,
    DateTimeOffset? LeftModified,
    DateTimeOffset? RightModified,
    IReadOnlyDictionary<string, string>? LeftHashes,
    IReadOnlyDictionary<string, string>? RightHashes,
    string? ErrorMessage
);
