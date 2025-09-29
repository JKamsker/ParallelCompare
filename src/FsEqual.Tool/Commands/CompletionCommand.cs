using Spectre.Console;
using Spectre.Console.Cli;

namespace FsEqual.Tool.Commands;

public sealed class CompletionCommand : Command<CompletionSettings>
{
    public override int Execute(CommandContext context, CompletionSettings settings)
    {
        var shell = settings.Shell.Trim().ToLowerInvariant();
        var script = shell switch
        {
            "bash" => BashScript,
            "zsh" => ZshScript,
            "fish" => FishScript,
            "powershell" or "pwsh" => PowerShellScript,
            _ => throw new CliUsageException($"Unsupported shell '{settings.Shell}'. Use bash, zsh, fish, or powershell.")
        };

        AnsiConsole.Write(script);
        return 0;
    }

    private const string BashScript = """
        _fsequal_completions()
        {
            local cur prev words
            COMPREPLY=()
            cur="${COMP_WORDS[COMP_CWORD]}"
            prev="${COMP_WORDS[COMP_CWORD-1]}"
            commands="compare watch snapshot completion"
            if [[ $COMP_CWORD -eq 1 ]]; then
                COMPREPLY=( $(compgen -W "$commands" -- $cur) )
                return 0
            fi
        }
        complete -F _fsequal_completions fsequal
        """;

    private const string ZshScript = """
        #compdef fsequal
        _arguments '1: :((compare\:Compare directories)(watch\:Watch for changes)(snapshot\:Create snapshots)(completion\:Generate completions))'
        """;

    private const string FishScript = """
        complete -c fsequal -n "not __fish_seen_subcommand_from compare watch snapshot completion" -a "compare" -d "Compare directories"
        complete -c fsequal -n "not __fish_seen_subcommand_from compare watch snapshot completion" -a "watch" -d "Watch for changes"
        complete -c fsequal -n "not __fish_seen_subcommand_from compare watch snapshot completion" -a "snapshot" -d "Work with snapshots"
        complete -c fsequal -n "not __fish_seen_subcommand_from compare watch snapshot completion" -a "completion" -d "Generate shell completion"
        """;

    private const string PowerShellScript = """
        Register-ArgumentCompleter -CommandName fsequal -ScriptBlock { param($commandName, $parameterName, $wordToComplete)
            $commands = 'compare','watch','snapshot','completion'
            foreach ($cmd in $commands) { if ($cmd -like "$wordToComplete*") { [System.Management.Automation.CompletionResult]::new($cmd,$cmd,'ParameterValue',$cmd) } }
        }
        """;
}
