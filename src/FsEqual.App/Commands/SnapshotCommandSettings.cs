using System;
using Spectre.Console.Cli;

namespace FsEqual.App.Commands;

/// <summary>
/// Extends <see cref="CompareCommandSettings"/> with options specific to the <c>snapshot</c> command.
/// </summary>
public sealed class SnapshotCommandSettings : CompareCommandSettings
{
    /// <summary>
    /// Gets the destination path for the generated baseline manifest.
    /// </summary>
    [CommandOption("--output <PATH>")]
    public string Output { get; init; } = string.Empty;

}
