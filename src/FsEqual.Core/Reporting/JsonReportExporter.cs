using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FsEqual.Core.Reporting;

/// <summary>
/// Writes the full comparison document to JSON.
/// </summary>
public sealed class JsonReportExporter : IReportExporter
{
    private readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    /// <inheritdoc />
    public string Format => ReportExportFormats.Json;

    /// <inheritdoc />
    public async Task ExportAsync(ReportExportContext context, CancellationToken cancellationToken)
    {
        var destination = EnsureDirectory(context.Destination);
        await using var stream = File.Create(destination);
        await JsonSerializer.SerializeAsync(stream, context.Document, _options, cancellationToken);
    }

    private static string EnsureDirectory(string path)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory());
        return fullPath;
    }
}
