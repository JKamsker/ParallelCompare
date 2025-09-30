using System.Collections.Immutable;
using ParallelCompare.Core.Options;

namespace ParallelCompare.Core.Comparison;

/// <summary>
/// Identifies whether a comparison node represents a directory or file.
/// </summary>
public enum ComparisonNodeType
{
    Directory,
    File
}

/// <summary>
/// Represents the outcome for a particular comparison node.
/// </summary>
public enum ComparisonStatus
{
    Equal,
    Different,
    LeftOnly,
    RightOnly,
    Error
}

/// <summary>
/// Contains detailed metadata for file comparisons, including hashes and timestamps.
/// </summary>
/// <param name="LeftSize">Size of the file in the left tree, if available.</param>
/// <param name="RightSize">Size of the file in the right tree, if available.</param>
/// <param name="LeftModified">Last modification timestamp for the left file.</param>
/// <param name="RightModified">Last modification timestamp for the right file.</param>
/// <param name="LeftHashes">Hashes calculated for the left file.</param>
/// <param name="RightHashes">Hashes calculated for the right file.</param>
/// <param name="ErrorMessage">Error message when the file could not be compared.</param>
public sealed record FileComparisonDetail(
    long? LeftSize,
    long? RightSize,
    DateTimeOffset? LeftModified,
    DateTimeOffset? RightModified,
    IReadOnlyDictionary<HashAlgorithmType, string>? LeftHashes,
    IReadOnlyDictionary<HashAlgorithmType, string>? RightHashes,
    string? ErrorMessage
);

/// <summary>
/// Represents a node within the comparison tree.
/// </summary>
/// <param name="Name">Display name for the entry.</param>
/// <param name="RelativePath">Path relative to the comparison root.</param>
/// <param name="NodeType">Type of node being represented.</param>
/// <param name="Status">Comparison status for the node.</param>
/// <param name="Detail">File-specific metadata when the node represents a file.</param>
/// <param name="Children">Child nodes when the node represents a directory.</param>
public sealed record ComparisonNode(
    string Name,
    string RelativePath,
    ComparisonNodeType NodeType,
    ComparisonStatus Status,
    FileComparisonDetail? Detail,
    ImmutableArray<ComparisonNode> Children
)
{
    /// <summary>
    /// Gets a value indicating whether the node or its children have differences.
    /// </summary>
    public bool HasDifferences => Status is ComparisonStatus.Different or ComparisonStatus.LeftOnly or ComparisonStatus.RightOnly or ComparisonStatus.Error;
}

/// <summary>
/// Summarizes the file-level outcomes for a comparison run.
/// </summary>
/// <param name="TotalFiles">Total number of files inspected.</param>
/// <param name="EqualFiles">Number of files that matched exactly.</param>
/// <param name="DifferentFiles">Number of files that differed.</param>
/// <param name="LeftOnlyFiles">Number of files only present on the left side.</param>
/// <param name="RightOnlyFiles">Number of files only present on the right side.</param>
/// <param name="ErrorFiles">Number of files that encountered errors during comparison.</param>
public sealed record ComparisonSummary(
    int TotalFiles,
    int EqualFiles,
    int DifferentFiles,
    int LeftOnlyFiles,
    int RightOnlyFiles,
    int ErrorFiles
);

/// <summary>
/// Represents the final output of a directory comparison.
/// </summary>
/// <param name="LeftPath">Absolute path to the left input directory.</param>
/// <param name="RightPath">Absolute path to the right input directory.</param>
/// <param name="Root">Root node describing the comparison tree.</param>
/// <param name="Summary">Summary metrics of the comparison.</param>
/// <param name="Baseline">Baseline metadata when the run was baseline-aware.</param>
public sealed record ComparisonResult(
    string LeftPath,
    string RightPath,
    ComparisonNode Root,
    ComparisonSummary Summary,
    BaselineMetadata? Baseline = null
);

/// <summary>
/// Provides context for baseline-aware comparison runs.
/// </summary>
/// <param name="ManifestPath">Path to the manifest used during baseline comparison.</param>
/// <param name="SourcePath">Original source directory captured in the baseline.</param>
/// <param name="CreatedAt">Timestamp for when the baseline was captured.</param>
/// <param name="Algorithms">Algorithms stored in the baseline manifest.</param>
public sealed record BaselineMetadata(
    string ManifestPath,
    string SourcePath,
    DateTimeOffset CreatedAt,
    ImmutableArray<HashAlgorithmType> Algorithms
);
