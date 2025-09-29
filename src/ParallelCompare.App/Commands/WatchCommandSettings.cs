using Spectre.Console.Cli;

namespace ParallelCompare.App.Commands;

public sealed class WatchCommandSettings : CompareCommandSettings
{
    [CommandOption("--debounce <MILLISECONDS>")]
    public int DebounceMilliseconds { get; init; } = 750;
}
