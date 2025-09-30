using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FsEqual.Core.Reporting;

/// <summary>
/// Writes difference entries to a CSV file, including metadata and summary headers.
/// </summary>
public sealed class CsvReportExporter : IReportExporter
{
    /// <inheritdoc />
    public string Format => ReportExportFormats.Csv;

    /// <inheritdoc />
    public async Task ExportAsync(ReportExportContext context, CancellationToken cancellationToken)
    {
        var destination = EnsureDirectory(context.Destination);
        var builder = new StringBuilder();

        WriteMetadata(builder, context);
        WriteSummary(builder, context);
        WriteDifferences(builder, context);

        await File.WriteAllTextAsync(destination, builder.ToString(), cancellationToken);
    }

    private static void WriteMetadata(StringBuilder builder, ReportExportContext context)
    {
        builder.AppendLine("# metadata");
        builder.AppendLine("key,value");
        builder.AppendLine($"generatedAt,{ReportTextUtilities.EscapeCsv(context.Document.Metadata.GeneratedAt.ToString("u"))}");
        builder.AppendLine($"leftPath,{ReportTextUtilities.EscapeCsv(context.Document.Metadata.LeftPath)}");
        builder.AppendLine($"rightPath,{ReportTextUtilities.EscapeCsv(context.Document.Metadata.RightPath)}");
        builder.AppendLine($"mode,{ReportTextUtilities.EscapeCsv(context.Document.Metadata.Mode)}");
        builder.AppendLine($"usesBaseline,{context.Document.Metadata.UsesBaseline.ToString().ToLowerInvariant()}");

        if (context.Document.Metadata.Algorithms.Length > 0)
        {
            var algorithms = string.Join(' ', context.Document.Metadata.Algorithms);
            builder.AppendLine($"algorithms,{ReportTextUtilities.EscapeCsv(algorithms)}");
        }

        if (!string.IsNullOrWhiteSpace(context.Document.Metadata.PrimaryAlgorithm))
        {
            builder.AppendLine($"primaryAlgorithm,{ReportTextUtilities.EscapeCsv(context.Document.Metadata.PrimaryAlgorithm!)}");
        }

        if (context.Document.Metadata.Baseline is not null)
        {
            builder.AppendLine($"baseline.manifest,{ReportTextUtilities.EscapeCsv(context.Document.Metadata.Baseline.ManifestPath)}");
            builder.AppendLine($"baseline.source,{ReportTextUtilities.EscapeCsv(context.Document.Metadata.Baseline.SourcePath)}");
            builder.AppendLine($"baseline.createdAt,{ReportTextUtilities.EscapeCsv(context.Document.Metadata.Baseline.CreatedAt.ToString("u"))}");
        }

        builder.AppendLine();
    }

    private static void WriteSummary(StringBuilder builder, ReportExportContext context)
    {
        builder.AppendLine("# summary");
        builder.AppendLine("metric,value");
        builder.AppendLine($"totalFiles,{context.Document.Summary.TotalFiles}");
        builder.AppendLine($"equalFiles,{context.Document.Summary.EqualFiles}");
        builder.AppendLine($"differentFiles,{context.Document.Summary.DifferentFiles}");
        builder.AppendLine($"leftOnlyFiles,{context.Document.Summary.LeftOnlyFiles}");
        builder.AppendLine($"rightOnlyFiles,{context.Document.Summary.RightOnlyFiles}");
        builder.AppendLine($"errorFiles,{context.Document.Summary.ErrorFiles}");
        builder.AppendLine();
    }

    private static void WriteDifferences(StringBuilder builder, ReportExportContext context)
    {
        builder.AppendLine("# differences");
        builder.AppendLine("Path,Type,Status,LeftSize,RightSize,LeftModified,RightModified,LeftHash,RightHash,Error");

        foreach (var difference in context.Document.Differences)
        {
            var detail = difference.Detail;
            var leftHash = ReportTextUtilities.SelectHash(detail?.LeftHashes, context.PreferredAlgorithm) ?? string.Empty;
            var rightHash = ReportTextUtilities.SelectHash(detail?.RightHashes, context.PreferredAlgorithm) ?? string.Empty;

            builder.AppendLine(string.Join(',', new[]
            {
                ReportTextUtilities.EscapeCsv(difference.Path),
                ReportTextUtilities.EscapeCsv(difference.Type),
                ReportTextUtilities.EscapeCsv(difference.Status),
                ReportTextUtilities.EscapeCsv(detail?.LeftSize?.ToString() ?? string.Empty),
                ReportTextUtilities.EscapeCsv(detail?.RightSize?.ToString() ?? string.Empty),
                ReportTextUtilities.EscapeCsv(detail?.LeftModified?.ToString("u") ?? string.Empty),
                ReportTextUtilities.EscapeCsv(detail?.RightModified?.ToString("u") ?? string.Empty),
                ReportTextUtilities.EscapeCsv(leftHash),
                ReportTextUtilities.EscapeCsv(rightHash),
                ReportTextUtilities.EscapeCsv(detail?.ErrorMessage ?? string.Empty)
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
