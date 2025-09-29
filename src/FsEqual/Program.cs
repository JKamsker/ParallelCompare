using Spectre.Console.Cli;
using Spectre.Console;
using FsEqual.Commands;

var app = new CommandApp();

app.Configure(config =>
{
    config.SetApplicationName("fsequal");
    config.PropagateExceptions();
    config.ValidateExamples();

    config.AddCommand<CompareCommand>("compare")
          .WithDescription("Compare two directories and report differences.")
          .WithExample(new[] { "compare", "./A", "./B" })
          .WithExample(new[] { "compare", "./A", "./B", "--interactive" })
          .WithExample(new[]
          {
              "compare",
              "./A",
              "./B",
              "-t", "8",
              "-a", "sha256",
              "-m", "hash",
              "-i", "**/bin/**",
              "-i", "**/obj/**"
          });
});

try
{
    return app.Run(args);
}
catch (CommandRuntimeException ex)
{
    AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
    AnsiConsole.WriteException(ex);
    return -2;
}
catch (Exception ex)
{
    AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
    AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
    return -2;
}
