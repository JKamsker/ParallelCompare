using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FsEqual.Core.Reporting;

/// <summary>
/// Placeholder exporter for future rich HTML reports.
/// </summary>
public sealed class HtmlReportExporter : IReportExporter
{
    /// <inheritdoc />
    public string Format => ReportExportFormats.Html;

    /// <inheritdoc />
    public async Task ExportAsync(ReportExportContext context, CancellationToken cancellationToken)
    {
        var destination = EnsureDirectory(context.Destination);
        var builder = new StringBuilder();
        builder.AppendLine("<!DOCTYPE html>");
        builder.AppendLine("<html lang=\"en\">");
        builder.AppendLine("<head>");
        builder.AppendLine("  <meta charset=\"utf-8\" />");
        builder.AppendLine("  <title>FsEqual Report (Preview)</title>");
        builder.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />");
        builder.AppendLine("  <style>body{font-family:Segoe UI,Helvetica,Arial,sans-serif;margin:2rem;}code{font-family:Consolas,monospace;}</style>");
        builder.AppendLine("</head>");
        builder.AppendLine("<body>");
        builder.AppendLine("  <h1>FsEqual Report</h1>");
        builder.AppendLine("  <p>This HTML exporter is a placeholder. Future releases will include an interactive experience.</p>");
        builder.AppendLine("  <h2>Summary</h2>");
        builder.AppendLine("  <pre>");
        builder.AppendLine(System.Text.Json.JsonSerializer.Serialize(
            new
            {
                metadata = context.Document.Metadata,
                summary = context.Document.Summary
            },
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)
            {
                WriteIndented = true
            }));
        builder.AppendLine("  </pre>");
        builder.AppendLine("</body>");
        builder.AppendLine("</html>");

        await File.WriteAllTextAsync(destination, builder.ToString(), cancellationToken);
    }

    private static string EnsureDirectory(string path)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory());
        return fullPath;
    }
}
