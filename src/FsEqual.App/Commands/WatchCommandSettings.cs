using Spectre.Console.Cli;

namespace FsEqual.App.Commands;

/// <summary>
/// Extends <see cref="CompareCommandSettings"/> with options specific to the <c>watch</c> command.
/// </summary>
public sealed class WatchCommandSettings : CompareCommandSettings
{
    /// <summary>
    /// Gets the debounce interval, in milliseconds, between watch-triggered reruns.
    /// </summary>
    [CommandOption("--debounce <MILLISECONDS>")]
    public int DebounceMilliseconds { get; init; } = 750;
}
