using Spectre.Console.Cli;

namespace ParallelCompare.App.Commands;

public sealed class CompletionCommandSettings : CommandSettings
{
    [CommandArgument(0, "<shell>")]
    public string Shell { get; init; } = "bash";
}
