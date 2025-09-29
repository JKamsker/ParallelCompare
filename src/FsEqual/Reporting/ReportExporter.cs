using System.IO;
using System.Text;
using System.Text.Json;
using FsEqual.Core;
using Spectre.Console;

namespace FsEqual.Reporting;

public sealed class ReportExporter
{
    public async Task ExportAsync(ComparisonResult result, ComparisonOptions options, IAnsiConsole console)
    {
        if (!string.IsNullOrWhiteSpace(options.JsonOutputPath))
        {
            await ExportJsonAsync(result, options.JsonOutputPath!, console);
        }

        if (!string.IsNullOrWhiteSpace(options.SummaryOutputPath))
        {
            await ExportSummaryAsync(result, options.SummaryOutputPath!, console);
        }
    }

    public async Task ExportAsync(ComparisonResult result, string path, string format, IAnsiConsole? console = null)
    {
        format = format.ToLowerInvariant();
        switch (format)
        {
            case "json":
                await ExportJsonAsync(result, path, console);
                break;
            case "csv":
                await ExportCsvAsync(result, path, console);
                break;
            case "markdown":
            case "md":
                await ExportMarkdownAsync(result, path, console);
                break;
            default:
                throw new InvalidOperationException($"Unknown export format '{format}'.");
        }
    }

    private static async Task ExportJsonAsync(ComparisonResult result, string path, IAnsiConsole? console)
    {
        var payload = new
        {
            generatedAt = DateTimeOffset.UtcNow,
            summary = result.Summary,
            differences = result.Differences,
            errors = result.Errors,
            elapsed = result.Elapsed
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await WriteFileAsync(path, json, console, "JSON report");
    }

    private static async Task ExportSummaryAsync(ComparisonResult result, string path, IAnsiConsole? console)
    {
        var json = JsonSerializer.Serialize(result.Summary, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        await WriteFileAsync(path, json, console, "summary report");
    }

    private static async Task ExportCsvAsync(ComparisonResult result, string path, IAnsiConsole? console)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Type,Path,LeftSize,RightSize,Algorithm,Reason");
        foreach (var diff in result.Differences)
        {
            builder.AppendLine(string.Join(',', new[]
            {
                Escape(diff.Type.ToString()),
                Escape(diff.Path),
                Escape(diff.LeftSize?.ToString() ?? string.Empty),
                Escape(diff.RightSize?.ToString() ?? string.Empty),
                Escape(diff.Algorithm ?? string.Empty),
                Escape(diff.Reason ?? string.Empty)
            }));
        }

        await WriteFileAsync(path, builder.ToString(), console, "CSV report");
    }

    private static async Task ExportMarkdownAsync(ComparisonResult result, string path, IAnsiConsole? console)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# FsEqual Differences");
        builder.AppendLine();
        builder.AppendLine($"Generated: {DateTimeOffset.UtcNow:O}");
        builder.AppendLine();
        builder.AppendLine("| Type | Path | Left Size | Right Size | Algorithm | Reason |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- |");
        foreach (var diff in result.Differences)
        {
            builder.AppendLine($"| {EscapeMarkdown(diff.Type.ToString())} | {EscapeMarkdown(diff.Path)} | {EscapeMarkdown(diff.LeftSize?.ToString() ?? string.Empty)} | {EscapeMarkdown(diff.RightSize?.ToString() ?? string.Empty)} | {EscapeMarkdown(diff.Algorithm ?? string.Empty)} | {EscapeMarkdown(diff.Reason ?? string.Empty)} |");
        }

        await WriteFileAsync(path, builder.ToString(), console, "Markdown report");
    }

    private static async Task WriteFileAsync(string path, string content, IAnsiConsole? console, string description)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? ".");
        await File.WriteAllTextAsync(fullPath, content);
        console?.MarkupLine($"[green]{description} written to[/] [cyan]{fullPath}[/]");
    }

    private static string Escape(string value)
    {
        return '"' + value.Replace("\"", "\"\"") + '"';
    }

    private static string EscapeMarkdown(string value)
    {
        return value.Replace("|", "\\|");
    }
}
