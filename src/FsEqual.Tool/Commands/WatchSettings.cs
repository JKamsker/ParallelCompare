using Spectre.Console.Cli;

namespace FsEqual.Tool.Commands;

public sealed class WatchSettings : CompareSettingsBase
{
    [CommandOption("--debounce <MS>")]
    public int DebounceMilliseconds { get; init; } = 750;
}
