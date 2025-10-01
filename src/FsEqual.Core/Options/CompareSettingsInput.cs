using System.Collections.Immutable;

namespace FsEqual.Core.Options;

/// <summary>
/// Represents raw compare command inputs prior to configuration resolution.
/// </summary>
public sealed record CompareSettingsInput
{
    /// <summary>
    /// Gets the required left input path.
    /// </summary>
    public required string LeftPath { get; init; }

    /// <summary>
    /// Gets the optional right input path.
    /// </summary>
    public string? RightPath { get; init; }

    /// <summary>
    /// Gets the comparison mode name supplied by the user.
    /// </summary>
    public string? Mode { get; init; }

    /// <summary>
    /// Gets the primary hash algorithm requested via command line.
    /// </summary>
    public string? Algorithm { get; init; }

    /// <summary>
    /// Gets additional hash algorithms specified via repeatable options.
    /// </summary>
    public ImmutableArray<string> AdditionalAlgorithms { get; init; } = ImmutableArray<string>.Empty;

    /// <summary>
    /// Gets glob patterns that should be ignored.
    /// </summary>
    public ImmutableArray<string> IgnorePatterns { get; init; } = ImmutableArray<string>.Empty;

    /// <summary>
    /// Gets a value indicating whether comparisons should be case sensitive.
    /// </summary>
    public bool? CaseSensitive { get; init; }

    /// <summary>
    /// Gets a value indicating whether symbolic links should be followed.
    /// </summary>
    public bool? FollowSymlinks { get; init; }

    /// <summary>
    /// Gets the modified time tolerance requested by the user.
    /// </summary>
    public TimeSpan? ModifiedTimeTolerance { get; init; }

    /// <summary>
    /// Gets the desired thread count for parallel execution.
    /// </summary>
    public int? Threads { get; init; }

    /// <summary>
    /// Gets the path to a baseline manifest when baseline comparison is requested.
    /// </summary>
    public string? BaselinePath { get; init; }

    /// <summary>
    /// Gets a value indicating whether the interactive UI should be enabled.
    /// </summary>
    public bool EnableInteractive { get; init; }

    /// <summary>
    /// Gets the output path for the JSON report exporter.
    /// </summary>
    public string? JsonReportPath { get; init; }

    /// <summary>
    /// Gets the output path for the summary report exporter.
    /// </summary>
    public string? SummaryReportPath { get; init; }

    /// <summary>
    /// Gets the output path for the CSV report exporter.
    /// </summary>
    public string? CsvReportPath { get; init; }

    /// <summary>
    /// Gets the output path for the Markdown report exporter.
    /// </summary>
    public string? MarkdownReportPath { get; init; }

    /// <summary>
    /// Gets the output path for the HTML report exporter.
    /// </summary>
    public string? HtmlReportPath { get; init; }

    /// <summary>
    /// Gets the named export format bundle to run.
    /// </summary>
    public string? ExportFormat { get; init; }

    /// <summary>
    /// Gets the summary filter applied when rendering console trees.
    /// </summary>
    public string? SummaryFilter { get; init; }

    /// <summary>
    /// Gets a value indicating whether progress output should be suppressed.
    /// </summary>
    public bool NoProgress { get; init; }

    /// <summary>
    /// Gets the diff tool command line requested.
    /// </summary>
    public string? DiffTool { get; init; }

    /// <summary>
    /// Gets the profile name to resolve from configuration files.
    /// </summary>
    public string? Profile { get; init; }

    /// <summary>
    /// Gets the explicit configuration file path.
    /// </summary>
    public string? ConfigurationPath { get; init; }

    /// <summary>
    /// Gets the verbosity level for console logging.
    /// </summary>
    public string? Verbosity { get; init; }

    /// <summary>
    /// Gets the failure condition expression (e.g., errors, differences).
    /// </summary>
    public string? FailOn { get; init; }

    /// <summary>
    /// Gets the optional timeout applied to the comparison.
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Gets the theme to apply to the interactive interface.
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
}
