using FsEqual.Tool.Comparison;
using Spectre.Console.Cli;

namespace FsEqual.Tool.Commands;

public sealed class CompareSettings : CompareSettingsBase
{
    [CommandOption("--json <PATH>")]
    public string? JsonOutput { get; init; }

    [CommandOption("--summary <PATH>")]
    public string? SummaryOutput { get; init; }

    [CommandOption("--baseline <PATH>")]
    public string? BaselinePath { get; init; }

    [CommandOption("--no-progress")]
    public bool NoProgress { get; init; }

    [CommandOption("--interactive")]
    public bool Interactive { get; init; }

    protected override string? GetBaselinePath() => BaselinePath;
}
