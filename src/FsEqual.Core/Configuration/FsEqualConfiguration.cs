using System.Text.Json.Serialization;

namespace FsEqual.Core.Configuration;

/// <summary>
/// Represents the root configuration file that defines defaults and named comparison profiles.
/// </summary>
public sealed record FsEqualConfiguration
{
    /// <summary>
    /// Gets the defaults applied when no profile is specified.
    /// </summary>
    [JsonPropertyName("defaults")]
    public CompareProfile Defaults { get; init; } = new();

    /// <summary>
    /// Gets the set of named profiles that can be resolved by command-line commands.
    /// </summary>
    [JsonPropertyName("profiles")]
    public Dictionary<string, CompareProfile> Profiles { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Describes a reusable set of comparison settings referenced by name.
/// </summary>
public class CompareProfile
{
    /// <summary>
    /// Gets the left input path when the profile is invoked.
    /// </summary>
    [JsonPropertyName("left")]
    public string? Left { get; init; }

    /// <summary>
    /// Gets the right input path when the profile is invoked.
    /// </summary>
    [JsonPropertyName("right")]
    public string? Right { get; init; }

    /// <summary>
    /// Gets the comparison mode name to apply when the profile is used.
    /// </summary>
    [JsonPropertyName("mode")]
    public string? Mode { get; init; }

    /// <summary>
    /// Gets the ordered list of hash algorithms to calculate.
    /// </summary>
    [JsonPropertyName("algorithms")]
    public string[]? Algorithms { get; init; }

    /// <summary>
    /// Gets the glob patterns that should be ignored.
    /// </summary>
    [JsonPropertyName("ignore")]
    public string[]? Ignore { get; init; }

    /// <summary>
    /// Gets a value indicating whether the comparison should be case sensitive.
    /// </summary>
    [JsonPropertyName("caseSensitive")]
    public bool? CaseSensitive { get; init; }

    /// <summary>
    /// Gets a value indicating whether symbolic links should be traversed.
    /// </summary>
    [JsonPropertyName("followSymlinks")]
    public bool? FollowSymlinks { get; init; }

    /// <summary>
    /// Gets the tolerance, in seconds, for modified timestamps.
    /// </summary>
    [JsonPropertyName("mtimeToleranceSeconds")]
    public double? MTimeToleranceSeconds { get; init; }

    /// <summary>
    /// Gets the maximum number of worker threads to use.
    /// </summary>
    [JsonPropertyName("threads")]
    public int? Threads { get; init; }

    /// <summary>
    /// Gets the baseline file path to compare against.
    /// </summary>
    [JsonPropertyName("baseline")]
    public string? Baseline { get; init; }

    /// <summary>
    /// Gets the output path for the JSON report exporter.
    /// </summary>
    [JsonPropertyName("json")]
    public string? JsonReport { get; init; }

    /// <summary>
    /// Gets the output path for the summary report exporter.
    /// </summary>
    [JsonPropertyName("summary")]
    public string? SummaryReport { get; init; }

    /// <summary>
    /// Gets the named export format bundle to invoke.
    /// </summary>
    [JsonPropertyName("export")]
    public string? ExportFormat { get; init; }

    /// <summary>
    /// Gets the summary filter applied when rendering console output.
    /// </summary>
    [JsonPropertyName("summaryFilter")]
    public string? SummaryFilter { get; init; }

    /// <summary>
    /// Gets a value indicating whether progress output should be suppressed.
    /// </summary>
    [JsonPropertyName("noProgress")]
    public bool? NoProgress { get; init; }

    /// <summary>
    /// Gets the diff tool command line to launch for file differences.
    /// </summary>
    [JsonPropertyName("diffTool")]
    public string? DiffTool { get; init; }

    /// <summary>
    /// Gets the verbosity level name to apply to logging.
    /// </summary>
    [JsonPropertyName("verbosity")]
    public string? Verbosity { get; init; }

    /// <summary>
    /// Gets the theme to apply to the interactive interface.
    /// </summary>
    [JsonPropertyName("interactiveTheme")]
    public string? InteractiveTheme { get; init; }

    /// <summary>
    /// Gets the filter expression applied to the interactive tree view.
    /// </summary>
    [JsonPropertyName("interactiveFilter")]
    public string? InteractiveFilter { get; init; }

    /// <summary>
    /// Gets the verbosity level used for interactive logging panes.
    /// </summary>
    [JsonPropertyName("interactiveVerbosity")]
    public string? InteractiveVerbosity { get; init; }
}
