using FsEqual.Tool.Commands;
using Spectre.Console;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(config =>
{
    config.SetApplicationName("fsequal");
    config.Settings.ApplicationVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.1.0";
    config.AddCommand<CompareCommand>("compare")
        .WithDescription("Compare two directories or a directory against a snapshot.")
        .WithExample(new[] { "compare", "./left", "./right" })
        .WithExample(new[] { "compare", "./left", "./right", "--algo", "sha256" })
        .WithExample(new[] { "compare", "./left", "--baseline", "baseline.json" })
        .WithExample(new[] { "compare", "./left", "./right", "--interactive" });
    config.AddCommand<WatchCommand>("watch")
        .WithDescription("Watch two directories and re-run comparison when files change.")
        .WithExample(new[] { "watch", "./left", "./right" });
    config.AddCommand<SnapshotCommand>("snapshot")
        .WithDescription("Capture a manifest snapshot of a directory.")
        .WithExample(new[] { "snapshot", "./left", "--output", "baseline.json" });
    config.AddCommand<CompletionCommand>("completion")
        .WithDescription("Generate shell completion scripts.")
        .WithExample(new[] { "completion", "bash" });
});

try
{
    return app.Run(args);
}
catch (Exception ex)
{
    AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
    return -1;
}
