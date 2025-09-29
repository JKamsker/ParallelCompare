using Spectre.Console.Cli;

namespace FsEqual.Tool.Commands;

public sealed class SnapshotSettings : CommandSettings
{
    [CommandArgument(0, "<PATH>")]
    public required string Target { get; init; }

    [CommandOption("--output <FILE>")]
    public string? Output { get; init; }

    [CommandOption("--compare <SNAPSHOT>")]
    public string? Compare { get; init; }

    [CommandOption("-a|--algo <ALGO>")]
    public string? Algorithm { get; init; }

    [CommandOption("-i|--ignore <GLOB>")]
    public string[]? Ignore { get; init; }

    [CommandOption("--case-sensitive")]
    public bool CaseSensitive { get; init; }

    [CommandOption("--follow-symlinks")]
    public bool FollowSymlinks { get; init; }

    [CommandOption("-t|--threads <THREADS>")]
    public int? Threads { get; init; }
}
