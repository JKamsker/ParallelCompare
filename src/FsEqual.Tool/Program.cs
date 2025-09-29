using FsEqual.Tool.Commands;
using Spectre.Console;
using Spectre.Console.Cli;

namespace FsEqual.Tool;

public static class Program
{
    public static int Main(string[] args)
    {
        var app = new CommandApp();
        app.Configure(config =>
        {
            config.SetApplicationName("fsequal");
            config.ValidateExamples();

            config.AddCommand<CompareCommand>("compare")
                .WithDescription("Compare two directories")
                .WithExample(new[] { "compare", "left", "right" })
                .WithExample(new[] { "compare", "A", "B", "-m", "hash", "-a", "sha256" })
                .WithExample(new[] { "compare", "A", "B", "--interactive" });

            config.AddCommand<WatchCommand>("watch")
                .WithDescription("Continuously compare directories when files change");

            config.AddCommand<SnapshotCommand>("snapshot")
                .WithDescription("Create or compare against directory snapshots");

            config.AddCommand<CompletionCommand>("completion")
                .WithDescription("Generate shell completion scripts");
        });

        try
        {
            return app.Run(args);
        }
        catch (CliUsageException ex)
        {
            AnsiConsole.MarkupLine($"[red]{ex.Message}[/]");
            return ex.ExitCode;
        }
        catch (CommandRuntimeException ex)
        {
            AnsiConsole.WriteException(ex);
            return -1;
        }
    }
}
