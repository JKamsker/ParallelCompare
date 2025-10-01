using System;
using Spectre.Console.Cli;

namespace FsEqual.App.Commands;

/// <summary>
/// Defines all command-line options accepted by the <c>compare</c> command.
/// </summary>
public class CompareCommandSettings : CommandSettings
{
    /// <summary>
    /// Gets the required left directory path.
    /// </summary>
    [CommandArgument(0, "<left>")]
    public string Left { get; init; } = string.Empty;

    /// <summary>
    /// Gets the optional right directory path. When omitted, a baseline must be provided.
    /// </summary>
    [CommandArgument(1, "[right]")]
    public string? Right { get; init; }

    /// <summary>
    /// Gets the desired maximum number of worker threads.
    /// </summary>
    [CommandOption("-t|--threads")]
    public int? Threads { get; init; }

    /// <summary>
    /// Gets the hash algorithm identifiers to compute (e.g. <c>crc32</c>, <c>sha256</c>).
    /// </summary>
    [CommandOption("-a|--algo")]
    public string[] Algorithms { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the comparison mode (quick or hash) requested by the user.
    /// </summary>
    [CommandOption("-m|--mode")]
    public string? Mode { get; init; }

    /// <summary>
    /// Gets the glob patterns that should be ignored during comparison.
    /// </summary>
    [CommandOption("-i|--ignore")]
    public string[] Ignore { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets a value indicating whether path comparisons should be case sensitive.
    /// </summary>
    [CommandOption("--case-sensitive")]
    public bool CaseSensitive { get; init; }

    /// <summary>
    /// Gets a value indicating whether symbolic links should be traversed.
    /// </summary>
    [CommandOption("--follow-symlinks")]
    public bool FollowSymlinks { get; init; }

    /// <summary>
    /// Gets the modified time tolerance, expressed in seconds.
    /// </summary>
    [CommandOption("--mtime-tolerance")]
    public double? ModifiedTimeToleranceSeconds { get; init; }

    /// <summary>
    /// Gets the path to a baseline manifest used for comparison.
    /// </summary>
    [CommandOption("--baseline")]
    public string? Baseline { get; init; }

    /// <summary>
    /// Gets the output path for the full JSON report.
    /// </summary>
    [CommandOption("--json")]
    public string? JsonReport { get; init; }

    /// <summary>
    /// Gets the output path for the summary JSON report.
    /// </summary>
    [CommandOption("--summary")]
    public string? SummaryReport { get; init; }

    /// <summary>
    /// Gets the named export bundle to execute after the comparison.
    /// </summary>
    [CommandOption("--export")]
    public string? ExportFormat { get; init; }

    /// <summary>
    /// Gets a value indicating whether progress output should be suppressed.
    /// </summary>
    [CommandOption("--no-progress")]
    public bool NoProgress { get; init; }

    /// <summary>
    /// Gets the diff tool command that should launch when inspecting differences.
    /// </summary>
    [CommandOption("--diff-tool")]
    public string? DiffTool { get; init; }

    /// <summary>
    /// Gets the configuration profile name to resolve from configuration files.
    /// </summary>
    [CommandOption("--profile")]
    public string? Profile { get; init; }

    /// <summary>
    /// Gets the path to a specific configuration file.
    /// </summary>
    [CommandOption("--config")]
    public string? ConfigurationPath { get; init; }

    /// <summary>
    /// Gets the summary filter applied when rendering tree output.
    /// </summary>
    [CommandOption("--summary-filter")]
    public string? SummaryFilter { get; init; }

    /// <summary>
    /// Gets the desired verbosity level for console output.
    /// </summary>
    [CommandOption("--verbosity")]
    public string? Verbosity { get; init; }

    /// <summary>
    /// Gets the failure condition expression used to determine the exit code.
    /// </summary>
    [CommandOption("--fail-on")]
    public string? FailOn { get; init; }

    /// <summary>
    /// Gets the overall timeout, in seconds, for the comparison run.
    /// </summary>
    [CommandOption("--timeout")]
    public int? TimeoutSeconds { get; init; }

    /// <summary>
    /// Gets a value indicating whether the interactive interface should start automatically.
    /// </summary>
    [CommandOption("--interactive")]
    public bool Interactive { get; init; }
}
