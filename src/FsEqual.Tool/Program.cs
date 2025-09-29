using FsEqual.Tool.Commands;
using FsEqual.Tool.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

var registrar = new TypeRegistrar();
var app = new CommandApp(registrar);

app.Configure(config =>
{
    config.SetApplicationName("fsequal");
    config.SetApplicationVersion("0.1.0");
    config.SetExceptionHandler((ex, _) =>
    {
        AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
        Environment.ExitCode = -2;
    });

    config.AddCommand<CompareCommand>("compare")
          .WithDescription("Compare directories and report differences.");

    config.AddCommand<CompletionCommand>("completion")
          .WithDescription("Generate shell completion scripts.");
});

return await app.RunAsync(args);
