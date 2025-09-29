using System;
using Spectre.Console.Cli;

namespace ParallelCompare.App.Commands;

public sealed class SnapshotCommandSettings : CompareCommandSettings
{
    [CommandOption("--output <PATH>")]
    public string Output { get; init; } = string.Empty;

}
