using Spectre.Console.Cli;

namespace FsEqual.App.Commands;

/// <summary>
/// Extends <see cref="CompareCommandSettings"/> with options specific to the <c>watch</c> command.
/// </summary>
public sealed class WatchCommandSettings : CompareCommandSettings
{
    /// <summary>
    /// Gets the optional debounce interval, in milliseconds, between watch-triggered reruns.
    /// Defaults to <see cref="DefaultDebounceMilliseconds"/> when not specified via CLI or configuration.
    /// </summary>
    [CommandOption("--debounce <MILLISECONDS>")]
    public int? DebounceMilliseconds { get; init; }

    /// <summary>
    /// Gets the default debounce interval applied when neither CLI nor configuration specify a value.
    /// </summary>
    public const int DefaultDebounceMilliseconds = 750;
}
