using Spectre.Console;
using Spectre.Console.Cli;

namespace FsEqual.App.Commands;

/// <summary>
/// Implements the <c>completion</c> command that prints shell completion scripts.
/// </summary>
public sealed class CompletionCommand : Command<CompletionCommandSettings>
{
    /// <inheritdoc />
    public override int Execute(CommandContext context, CompletionCommandSettings settings)
    {
        var script = GenerateScript(settings.Shell);
        AnsiConsole.Write(script);
        return 0;
    }

    private static string GenerateScript(string shell)
    {
        return shell.ToLowerInvariant() switch
        {
            "bash" => BashScript,
            "zsh" => ZshScript,
            "pwsh" or "powershell" => PwshScript,
            _ => BashScript
        };
    }

    private const string BashScript = "_fsequal_completions()\n" +
        "{\n" +
        "    local cur prev words cword\n" +
        "    _init_completion -n : || return\n" +
        "    COMPREPLY=($(compgen -W \"compare watch snapshot completion --help --version\" -- \"$cur\"))\n" +
        "}\n" +
        "complete -F _fsequal_completions fsequal\n";

    private const string ZshScript = "#compdef fsequal\n" +
        "_fsequal_completions() {\n" +
        "  _arguments '1: :((compare watch snapshot completion --help --version))'\n" +
        "}\n" +
        "compdef _fsequal_completions fsequal\n";

    private const string PwshScript = "Register-ArgumentCompleter -CommandName fsequal -ScriptBlock {\n" +
        "    param($commandName, $parameterName, $wordToComplete)\n" +
        "    'compare','watch','snapshot','completion','--help','--version' | Where-Object { $_ -like \"$wordToComplete*\" } | ForEach-Object {\n" +
        "        [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', $_)\n" +
        "    }\n" +
        "}\n";
}
