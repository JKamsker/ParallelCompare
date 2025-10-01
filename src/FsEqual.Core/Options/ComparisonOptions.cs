using System.Collections.Immutable;
using FsEqual.Core.Comparison;
using FsEqual.Core.FileSystem;

namespace FsEqual.Core.Options;

/// <summary>
/// Configures a comparison run executed by the <see cref="ComparisonEngine"/>.
/// </summary>
public sealed record ComparisonOptions
{
    /// <summary>
    /// Gets the left input path.
    /// </summary>
    public required string LeftPath { get; init; }

    /// <summary>
    /// Gets the right input path.
    /// </summary>
    public required string RightPath { get; init; }

    /// <summary>
    /// Gets the comparison mode.
    /// </summary>
    public ComparisonMode Mode { get; init; } = ComparisonMode.Quick;

    /// <summary>
    /// Gets the hash algorithms to compute during comparison.
    /// </summary>
    public ImmutableArray<HashAlgorithmType> HashAlgorithms { get; init; } = ImmutableArray<HashAlgorithmType>.Empty;

    /// <summary>
    /// Gets ignore patterns applied to both directory trees.
    /// </summary>
    public ImmutableArray<string> IgnorePatterns { get; init; } = ImmutableArray<string>.Empty;

    /// <summary>
    /// Gets a value indicating whether comparisons are case sensitive.
    /// </summary>
    public bool CaseSensitive { get; init; }

    /// <summary>
    /// Gets a value indicating whether symbolic links are followed.
    /// </summary>
    public bool FollowSymlinks { get; init; }

    /// <summary>
    /// Gets the tolerance for file modified timestamps.
    /// </summary>
    public TimeSpan? ModifiedTimeTolerance { get; init; }

    /// <summary>
    /// Gets the maximum number of worker threads.
    /// </summary>
    public int? MaxDegreeOfParallelism { get; init; }

    /// <summary>
    /// Gets the baseline manifest path when comparing against a baseline.
    /// </summary>
    public string? BaselinePath { get; init; }

    /// <summary>
    /// Gets a value indicating whether interactive mode is enabled.
    /// </summary>
    public bool EnableInteractive { get; init; }

    /// <summary>
    /// Gets the JSON report output path.
    /// </summary>
    public string? JsonReportPath { get; init; }

    /// <summary>
    /// Gets the summary report output path.
    /// </summary>
    public string? SummaryReportPath { get; init; }

    /// <summary>
    /// Gets the CSV report output path.
    /// </summary>
    public string? CsvReportPath { get; init; }

    /// <summary>
    /// Gets the Markdown report output path.
    /// </summary>
    public string? MarkdownReportPath { get; init; }

    /// <summary>
    /// Gets the HTML report output path.
    /// </summary>
    public string? HtmlReportPath { get; init; }

    /// <summary>
    /// Gets the named export format bundle to execute.
    /// </summary>
    public string? ExportFormat { get; init; }

    /// <summary>
    /// Gets a value indicating whether progress output is suppressed.
    /// </summary>
    public bool NoProgress { get; init; }

    /// <summary>
    /// Gets the diff tool command line to launch for file differences.
    /// </summary>
    public string? DiffTool { get; init; }

    /// <summary>
    /// Gets the update sink notified during comparison progress.
    /// </summary>
    public IComparisonUpdateSink? UpdateSink { get; init; }

    /// <summary>
    /// Gets the progress sink notified as the comparison executes.
    /// </summary>
    public IComparisonProgressSink? ProgressSink { get; init; }

    /// <summary>
    /// Gets the cancellation token observed during execution.
    /// </summary>
    public CancellationToken CancellationToken { get; init; } = CancellationToken.None;

    /// <summary>
    /// Gets the file system abstraction used to access files.
    /// </summary>
    public IFileSystem FileSystem { get; init; } = PhysicalFileSystem.Instance;
}
