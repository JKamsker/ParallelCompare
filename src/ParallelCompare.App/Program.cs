using Microsoft.Extensions.DependencyInjection;
using ParallelCompare.App.Commands;
using ParallelCompare.App.Infrastructure;
using ParallelCompare.App.Services;
using Spectre.Console.Cli;

var services = new ServiceCollection();
services.AddSingleton<ComparisonOrchestrator>();

var registrar = new TypeRegistrar(services);
var app = new CommandApp(registrar);

app.Configure(config =>
{
    config.SetApplicationName("fsequal");
    config.ValidateExamples();

    config.AddCommand<CompareCommand>("compare")
        .WithDescription("Compare two directories and report differences.");

    config.AddCommand<WatchCommand>("watch")
        .WithDescription("Continuously compare directories and refresh on changes.");

    config.AddCommand<SnapshotCommand>("snapshot")
        .WithDescription("Create a snapshot report of the current comparison.");

    config.AddCommand<CompletionCommand>("completion")
        .WithDescription("Generate shell completion scripts.");
});

return app.Run(args);
