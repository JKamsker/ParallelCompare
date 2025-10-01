using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FsEqual.Core.Reporting;

/// <summary>
/// Writes a human-readable Markdown report.
/// </summary>
public sealed class MarkdownReportExporter : IReportExporter
{
    /// <inheritdoc />
    public string Format => ReportExportFormats.Markdown;

    /// <inheritdoc />
    public async Task ExportAsync(ReportExportContext context, CancellationToken cancellationToken)
    {
        var destination = EnsureDirectory(context.Destination);
        var builder = new StringBuilder();

        WriteHeader(builder, context);
        WriteSummary(builder, context);
        WriteDifferences(builder, context);

        await File.WriteAllTextAsync(destination, builder.ToString(), cancellationToken);
    }

    private static void WriteHeader(StringBuilder builder, ReportExportContext context)
    {
        builder.AppendLine("# FsEqual Report");
        builder.AppendLine();
        builder.AppendLine($"- Left: `{ReportTextUtilities.EscapeMarkdown(context.Document.Metadata.LeftPath)}`");
        builder.AppendLine($"- Right: `{ReportTextUtilities.EscapeMarkdown(context.Document.Metadata.RightPath)}`");
        builder.AppendLine($"- Mode: `{ReportTextUtilities.EscapeMarkdown(context.Document.Metadata.Mode)}`");
        builder.AppendLine($"- Generated: {context.Document.Metadata.GeneratedAt:u}");

        if (context.Document.Metadata.Algorithms.Length > 0)
        {
            var algorithms = string.Join(", ", context.Document.Metadata.Algorithms);
            builder.AppendLine($"- Algorithms: `{ReportTextUtilities.EscapeMarkdown(algorithms)}`");
        }

        if (!string.IsNullOrWhiteSpace(context.Document.Metadata.PrimaryAlgorithm))
        {
            builder.AppendLine($"- Preferred Hash: `{ReportTextUtilities.EscapeMarkdown(context.Document.Metadata.PrimaryAlgorithm!)}`");
        }

        if (context.Document.Metadata.Baseline is not null)
        {
            builder.AppendLine($"- Baseline Manifest: `{ReportTextUtilities.EscapeMarkdown(context.Document.Metadata.Baseline.ManifestPath)}`");
            builder.AppendLine($"- Baseline Source: `{ReportTextUtilities.EscapeMarkdown(context.Document.Metadata.Baseline.SourcePath)}`");
            builder.AppendLine($"- Baseline Captured: {context.Document.Metadata.Baseline.CreatedAt:u}");
        }

        builder.AppendLine();
    }

    private static void WriteSummary(StringBuilder builder, ReportExportContext context)
    {
        builder.AppendLine("## Summary");
        builder.AppendLine();
        builder.AppendLine("| Metric | Value |");
        builder.AppendLine("| --- | --- |");
        builder.AppendLine($"| Total Files | {context.Document.Summary.TotalFiles} |");
        builder.AppendLine($"| Equal Files | {context.Document.Summary.EqualFiles} |");
        builder.AppendLine($"| Different Files | {context.Document.Summary.DifferentFiles} |");
        builder.AppendLine($"| Left Only Files | {context.Document.Summary.LeftOnlyFiles} |");
        builder.AppendLine($"| Right Only Files | {context.Document.Summary.RightOnlyFiles} |");
        builder.AppendLine($"| Error Files | {context.Document.Summary.ErrorFiles} |");
        builder.AppendLine();
    }

    private static void WriteDifferences(StringBuilder builder, ReportExportContext context)
    {
        builder.AppendLine("## Differences");
        builder.AppendLine();

        if (context.Document.Differences.Length == 0)
        {
            builder.AppendLine("No differences were detected.");
            return;
        }

        builder.AppendLine("| Path | Type | Status | Left Size | Right Size | Left Modified | Right Modified | Left Hash | Right Hash | Error |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |");

        foreach (var difference in context.Document.Differences)
        {
            var detail = difference.Detail;
            var leftHash = ReportTextUtilities.SelectHash(detail?.LeftHashes, context.PreferredAlgorithm) ?? string.Empty;
            var rightHash = ReportTextUtilities.SelectHash(detail?.RightHashes, context.PreferredAlgorithm) ?? string.Empty;

            builder.AppendLine(string.Join(" | ", new[]
            {
                ReportTextUtilities.EscapeMarkdown(difference.Path),
                ReportTextUtilities.EscapeMarkdown(difference.Type),
                ReportTextUtilities.EscapeMarkdown(difference.Status),
                ReportTextUtilities.EscapeMarkdown(detail?.LeftSize?.ToString() ?? string.Empty),
                ReportTextUtilities.EscapeMarkdown(detail?.RightSize?.ToString() ?? string.Empty),
                ReportTextUtilities.EscapeMarkdown(detail?.LeftModified?.ToString("u") ?? string.Empty),
                ReportTextUtilities.EscapeMarkdown(detail?.RightModified?.ToString("u") ?? string.Empty),
                ReportTextUtilities.EscapeMarkdown(leftHash),
                ReportTextUtilities.EscapeMarkdown(rightHash),
                ReportTextUtilities.EscapeMarkdown(detail?.ErrorMessage ?? string.Empty)
            }));
        }
    }

    private static string EnsureDirectory(string path)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory());
        return fullPath;
    }
}
