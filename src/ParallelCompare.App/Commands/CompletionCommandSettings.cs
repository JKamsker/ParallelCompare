using Spectre.Console.Cli;

namespace ParallelCompare.App.Commands;

/// <summary>
/// Defines options for the <c>completion</c> command that emits shell completions.
/// </summary>
public sealed class CompletionCommandSettings : CommandSettings
{
    /// <summary>
    /// Gets the shell identifier (e.g. <c>bash</c>, <c>zsh</c>) to generate scripts for.
    /// </summary>
    [CommandArgument(0, "<shell>")]
    public string Shell { get; init; } = "bash";
}
