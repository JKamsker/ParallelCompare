using System;
using Spectre.Console.Cli;

namespace ParallelCompare.App.Commands;

public class CompareCommandSettings : CommandSettings
{
    [CommandArgument(0, "<left>")]
    public string Left { get; init; } = string.Empty;

    [CommandArgument(1, "[right]")]
    public string? Right { get; init; }

    [CommandOption("-t|--threads")]
    public int? Threads { get; init; }

    [CommandOption("-a|--algo")]
    public string[] Algorithms { get; init; } = Array.Empty<string>();

    [CommandOption("-m|--mode")]
    public string? Mode { get; init; }

    [CommandOption("-i|--ignore")]
    public string[] Ignore { get; init; } = Array.Empty<string>();

    [CommandOption("--case-sensitive")]
    public bool CaseSensitive { get; init; }

    [CommandOption("--follow-symlinks")]
    public bool FollowSymlinks { get; init; }

    [CommandOption("--mtime-tolerance")]
    public double? ModifiedTimeToleranceSeconds { get; init; }

    [CommandOption("--baseline")]
    public string? Baseline { get; init; }

    [CommandOption("--json")]
    public string? JsonReport { get; init; }

    [CommandOption("--summary")]
    public string? SummaryReport { get; init; }

    [CommandOption("--export")]
    public string? ExportFormat { get; init; }

    [CommandOption("--no-progress")]
    public bool NoProgress { get; init; }

    [CommandOption("--diff-tool")]
    public string? DiffTool { get; init; }

    [CommandOption("--profile")]
    public string? Profile { get; init; }

    [CommandOption("--config")]
    public string? ConfigurationPath { get; init; }

    [CommandOption("--verbosity")]
    public string? Verbosity { get; init; }

    [CommandOption("--fail-on")]
    public string? FailOn { get; init; }

    [CommandOption("--timeout")]
    public int? TimeoutSeconds { get; init; }

    [CommandOption("--interactive")]
    public bool Interactive { get; init; }
}
