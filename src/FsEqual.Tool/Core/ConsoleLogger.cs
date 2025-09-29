using Spectre.Console;

namespace FsEqual.Tool.Core;

internal sealed class ConsoleLogger
{
    private readonly VerbosityLevel _verbosity;

    public ConsoleLogger(VerbosityLevel verbosity)
    {
        _verbosity = verbosity;
    }

    public void Log(VerbosityLevel level, string message)
    {
        if ((int)level < (int)_verbosity)
        {
            return;
        }

        var prefix = level switch
        {
            VerbosityLevel.Trace => "[grey][[trace]][/]",
            VerbosityLevel.Debug => "[grey][[debug]][/]",
            VerbosityLevel.Info => "[blue][[info]][/]",
            VerbosityLevel.Warn => "[yellow][[warn]][/]",
            VerbosityLevel.Error => "[red][[error]][/]",
            _ => string.Empty,
        };

        AnsiConsole.MarkupLine($"{prefix} {Markup.Escape(message)}");
    }

    public void LogException(string scope, Exception exception)
    {
        if ((int)_verbosity > (int)VerbosityLevel.Error)
        {
            return;
        }

        AnsiConsole.MarkupLineInterpolated($"[red][[error]][/] {Markup.Escape(scope)}: {Markup.Escape(exception.Message)}");
        if (_verbosity <= VerbosityLevel.Debug)
        {
            AnsiConsole.WriteException(exception, ExceptionFormats.ShortenPaths | ExceptionFormats.ShortenTypes);
        }
    }
}
