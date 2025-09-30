using System.Collections.Immutable;
using FsEqual.Core.Comparison;
using FsEqual.Core.FileSystem;

namespace FsEqual.Core.Options;

/// <summary>
/// Represents normalized comparison settings after merging configuration and command-line input.
/// </summary>
public sealed record ResolvedCompareSettings
{
    /// <summary>
    /// Gets the left input path used for the comparison.
    /// </summary>
    public required string LeftPath { get; init; }

    /// <summary>
    /// Gets the optional right input path.
    /// </summary>
    public string? RightPath { get; init; }

    /// <summary>
    /// Gets the comparison mode to execute.
    /// </summary>
    public ComparisonMode Mode { get; init; }

    /// <summary>
    /// Gets the hash algorithms that should be computed.
    /// </summary>
    public ImmutableArray<HashAlgorithmType> Algorithms { get; init; } = ImmutableArray<HashAlgorithmType>.Empty;

    /// <summary>
    /// Gets the ignore patterns applied to both trees.
    /// </summary>
    public ImmutableArray<string> IgnorePatterns { get; init; } = ImmutableArray<string>.Empty;

    /// <summary>
    /// Gets a value indicating whether comparisons should be case sensitive.
    /// </summary>
    public bool CaseSensitive { get; init; }

    /// <summary>
    /// Gets a value indicating whether symbolic links should be traversed.
    /// </summary>
    public bool FollowSymlinks { get; init; }

    /// <summary>
    /// Gets the modified time tolerance window.
    /// </summary>
    public TimeSpan? ModifiedTimeTolerance { get; init; }

    /// <summary>
    /// Gets the maximum degree of parallelism.
    /// </summary>
    public int? Threads { get; init; }

    /// <summary>
    /// Gets the baseline manifest path when baseline comparison is requested.
    /// </summary>
    public string? BaselinePath { get; init; }

    /// <summary>
    /// Gets the JSON report output path.
    /// </summary>
    public string? JsonReportPath { get; init; }

    /// <summary>
    /// Gets the summary report output path.
    /// </summary>
    public string? SummaryReportPath { get; init; }

    /// <summary>
    /// Gets the named export format bundle.
    /// </summary>
    public string? ExportFormat { get; init; }

    /// <summary>
    /// Gets a value indicating whether progress output should be suppressed.
    /// </summary>
    public bool NoProgress { get; init; }

    /// <summary>
    /// Gets the diff tool command configured for file differences.
    /// </summary>
    public string? DiffTool { get; init; }

    /// <summary>
    /// Gets the console verbosity level.
    /// </summary>
    public string? Verbosity { get; init; }

    /// <summary>
    /// Gets the failure condition expression used to determine exit codes.
    /// </summary>
    public string? FailOn { get; init; }

    /// <summary>
    /// Gets the overall timeout for the comparison run.
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Gets the theme used by the interactive experience.
    /// </summary>
    public string? InteractiveTheme { get; init; }

    /// <summary>
    /// Gets the filter applied to the interactive tree view.
    /// </summary>
    public string? InteractiveFilter { get; init; }

    /// <summary>
    /// Gets the verbosity level for interactive logging panes.
    /// </summary>
    public string? InteractiveVerbosity { get; init; }

    /// <summary>
    /// Gets a value indicating whether the run uses baseline metadata.
    /// </summary>
    public bool UsesBaseline { get; init; }

    /// <summary>
    /// Gets metadata describing the baseline when applicable.
    /// </summary>
    public BaselineMetadata? BaselineMetadata { get; init; }

    /// <summary>
    /// Gets the debounce interval, in milliseconds, applied when watching for changes.
    /// </summary>
    public int? WatchDebounceMilliseconds { get; init; }

    /// <summary>
    /// Gets the file system abstraction used by the comparison engine.
    /// </summary>
    public IFileSystem FileSystem { get; init; } = PhysicalFileSystem.Instance;
}
