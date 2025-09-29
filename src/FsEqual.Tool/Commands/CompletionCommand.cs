using Spectre.Console;
using Spectre.Console.Cli;

namespace FsEqual.Tool.Commands;

public sealed class CompletionCommand : Command<CompletionCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<SHELL>")]
        public string Shell { get; set; } = string.Empty;
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var script = settings.Shell.ToLowerInvariant() switch
        {
            "bash" => Bash,
            "zsh" => Zsh,
            "fish" => Fish,
            "powershell" or "pwsh" => PowerShell,
            _ => throw new InvalidOperationException($"Unsupported shell '{settings.Shell}'."),
        };

        AnsiConsole.Write(script);
        return 0;
    }

    private const string Bash = @"_fsequal_complete() {
    COMPREPLY=()
    local cur=""${COMP_WORDS[COMP_CWORD]}""
    local commands=""compare watch snapshot completion""
    if [[ $COMP_CWORD -eq 1 ]]; then
        COMPREPLY=( $(compgen -W ""$commands"" -- $cur) )
    fi
}
complete -F _fsequal_complete fsequal
";

    private const string Zsh = @"#compdef fsequal
_fsequal_complete() {
    local -a commands
    commands=(compare watch snapshot completion)
    _arguments '1:command:(( ${commands[@]} ))'
}
_fsequal_complete ""$@""
";

    private const string Fish = @"function __fsequal_complete
    set -l cmd (commandline -opc)
    switch (count $cmd)
        case 1
            printf 'compare
watch
snapshot
completion
'
    end
end
complete -c fsequal -f -a 'compare watch snapshot completion'
";

    private const string PowerShell = @"Register-ArgumentCompleter -CommandName fsequal -ScriptBlock {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameters)
    'compare','watch','snapshot','completion' | Where-Object { $_ -like ""$wordToComplete*"" } | ForEach-Object { [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', $_) }
}
";
}
