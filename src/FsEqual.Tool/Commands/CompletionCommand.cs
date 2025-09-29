using Spectre.Console;
using Spectre.Console.Cli;

namespace FsEqual.Tool.Commands;

internal sealed class CompletionCommand : Command<CompletionCommand.Settings>
{
    public override int Execute(CommandContext context, Settings settings)
    {
        var shell = settings.Shell.ToLowerInvariant();
        var script = shell switch
        {
            "bash" => Bash,
            "zsh" => Zsh,
            "fish" => Fish,
            "powershell" or "pwsh" => PowerShell,
            _ => null,
        };

        if (script is null)
        {
            AnsiConsole.MarkupLine($"[red]Unsupported shell '{settings.Shell}'. Supported shells: bash, zsh, fish, powershell.[/]");
            return -1;
        }

        AnsiConsole.WriteLine(script);
        return 0;
    }

    private const string Keywords = "compare completion --threads --algo --mode --ignore --case-sensitive --follow-symlinks --mtime-tolerance --verbosity --json --summary --no-progress --interactive --timeout --fail-on --profile --config";

    private static readonly string Bash = string.Format("_fsequal_completions() {{\n    local cur=${{COMP_WORDS[COMP_CWORD]}}\n    COMPREPLY=( $( compgen -W \"{0}\" -- \"$cur\" ) )\n}}\ncomplete -F _fsequal_completions fsequal", Keywords);

    private static readonly string Zsh = string.Format("#compdef fsequal\n_arguments '*: :->args'\ncase $state in\n  args)\n    _values 'fsequal' {0}\n  ;;\nesac", Keywords);

    private static readonly string Fish = string.Format("complete -c fsequal -n 'not __fish_seen_subcommand_from compare completion' -a \"{0}\"", Keywords);

    private static readonly string PowerShell = string.Format("Register-ArgumentCompleter -CommandName fsequal -ScriptBlock {{\n    param($commandName, $parameterName, $wordToComplete)\n    $keywords = @('{0}'.Split(' '))\n    $keywords | Where-Object {{ $_ -like \"$wordToComplete*\" }} | ForEach-Object {{\n        [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', $_)\n    }}\n}}", Keywords);

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<shell>")]
        public string Shell { get; set; } = "bash";
    }
}
