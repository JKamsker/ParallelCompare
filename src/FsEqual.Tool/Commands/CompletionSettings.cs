using Spectre.Console.Cli;

namespace FsEqual.Tool.Commands;

public sealed class CompletionSettings : CommandSettings
{
    [CommandArgument(0, "<SHELL>")]
    public required string Shell { get; init; }
}
