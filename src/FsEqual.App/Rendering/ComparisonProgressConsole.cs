using System;
using System.Threading;
using System.Threading.Tasks;
using FsEqual.App.Services;
using FsEqual.Core.Options;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace FsEqual.App.Rendering;

/// <summary>
/// Renders a live progress view while a comparison run executes.
/// </summary>
public static class ComparisonProgressConsole
{
    /// <summary>
    /// Executes the comparison with live progress output.
    /// </summary>
    /// <param name="orchestrator">Comparison orchestrator to invoke.</param>
    /// <param name="input">Comparison input describing the run.</param>
    /// <param name="cancellationToken">Token used to cancel execution.</param>
    /// <returns>The completed comparison result and resolved settings.</returns>
    public static async Task<(FsEqual.Core.Comparison.ComparisonResult Result, ResolvedCompareSettings Resolved)> RunAsync(
        ComparisonOrchestrator orchestrator,
        CompareSettingsInput input,
        CancellationToken cancellationToken)
    {
        var tracker = new ComparisonProgressTracker();
        var initial = BuildProgressPanel(tracker.GetSnapshot());

        FsEqual.Core.Comparison.ComparisonResult? result = null;
        ResolvedCompareSettings? resolved = null;

        await AnsiConsole.Live(initial)
            .AutoClear(false)
            .StartAsync(async ctx =>
            {
                var runTask = orchestrator.RunAsync(input, cancellationToken, tracker);

                while (!runTask.IsCompleted)
                {
                    ctx.UpdateTarget(BuildProgressPanel(tracker.GetSnapshot()));
                    ctx.Refresh();

                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    await Task.WhenAny(runTask, Task.Delay(100));
                }

                var run = await runTask;
                result = run.Result;
                resolved = run.Resolved;
                ctx.UpdateTarget(BuildProgressPanel(tracker.GetSnapshot()));
                ctx.Refresh();
            });

        return (result!, resolved!);
    }

    private static IRenderable BuildProgressPanel(ComparisonProgressSnapshot snapshot)
    {
        var table = new Table().Title("[bold]Comparison Progress[/]");
        table.AddColumn("Metric");
        table.AddColumn(new TableColumn("Value").RightAligned());

        table.AddRow("Left MB/s", FormatThroughput(snapshot.LeftBytesPerSecond));
        table.AddRow("Right MB/s", FormatThroughput(snapshot.RightBytesPerSecond));
        table.AddRow("Files/s", FormatFilesPerSecond(snapshot.FilesPerSecond));
        table.AddRow("Processed Files", snapshot.CompletedFiles.ToString());
        table.AddRow("Total Read", FormatBytes(snapshot.TotalBytes));
        table.AddRow("Pipeline Files", snapshot.PendingFiles.ToString());

        return new Panel(table)
            .Border(BoxBorder.Rounded)
            .Padding(1, 0)
            .BorderColor(Color.Grey);
    }

    private static string FormatThroughput(double bytesPerSecond)
    {
        var mbPerSecond = bytesPerSecond / (1024d * 1024d);
        return $"{mbPerSecond:0.00} MB/s";
    }

    private static string FormatFilesPerSecond(double filesPerSecond)
        => $"{filesPerSecond:0.00}";

    private static string FormatBytes(long bytes)
    {
        if (bytes == 0)
        {
            return "0 B";
        }

        var absValue = Math.Abs((double)bytes);
        var units = new[] { "B", "KB", "MB", "GB", "TB", "PB" };
        var unitIndex = 0;

        while (absValue >= 1024 && unitIndex < units.Length - 1)
        {
            absValue /= 1024;
            unitIndex++;
        }

        var sign = bytes < 0 ? "-" : string.Empty;
        return $"{sign}{absValue:0.##} {units[unitIndex]}";
    }
}
