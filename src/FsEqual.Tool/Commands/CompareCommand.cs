using FsEqual.Tool.Comparison;
using FsEqual.Tool.Interactive;
using FsEqual.Tool.Reporting;
using Spectre.Console;
using Spectre.Console.Cli;

namespace FsEqual.Tool.Commands;

public sealed class CompareCommand : AsyncCommand<CompareSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, CompareSettings settings)
    {
        var options = settings.ToOptions();
        using var cts = new CancellationTokenSource();
        if (options.Timeout is { } timeout)
        {
            cts.CancelAfter(timeout);
        }

        void OnCancel(object? sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            cts.Cancel();
        }

        Console.CancelKeyPress += OnCancel;

        try
        {
            var comparer = new DirectoryComparer();
            ComparisonResult result;

            if (settings.NoProgress || settings.Interactive)
            {
                result = await comparer.CompareAsync(options, null, cts.Token).ConfigureAwait(false);
            }
            else
            {
                result = await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .StartAsync("Comparing...", async ctx =>
                    {
                        var progress = new Progress<ComparerProgress>(update =>
                        {
                            if (!string.IsNullOrWhiteSpace(update.Message))
                            {
                                ctx.Status(update.Message);
                            }
                        });

                        return await comparer.CompareAsync(options, progress, cts.Token).ConfigureAwait(false);
                    });
            }

            if (settings.Interactive)
            {
                if (!string.IsNullOrWhiteSpace(settings.JsonOutput) || !string.IsNullOrWhiteSpace(settings.SummaryOutput))
                {
                    AnsiConsole.MarkupLine("[yellow]JSON and summary outputs are disabled in interactive mode. Use the Export action instead.[/]");
                }

                var session = new InteractiveCompareSession(result, options);
                session.Run();
            }
            else
            {
                var reporter = new CompareReporter(options, settings);
                reporter.Render(result);
            }

            return ComparisonExitCodes.Calculate(options, result);
        }
        catch (CliUsageException ex)
        {
            AnsiConsole.MarkupLine($"[red]{ex.Message}[/]");
            return ex.ExitCode;
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[red]Comparison cancelled.[/]");
            return 2;
        }
        finally
        {
            Console.CancelKeyPress -= OnCancel;
        }
    }
}
