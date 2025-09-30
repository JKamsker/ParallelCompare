using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FsEqual.Core.Reporting;

/// <summary>
/// Writes a concise JSON export containing metadata and summary information.
/// </summary>
public sealed class SummaryReportExporter : IReportExporter
{
    private readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    /// <inheritdoc />
    public string Format => ReportExportFormats.Summary;

    /// <inheritdoc />
    public async Task ExportAsync(ReportExportContext context, CancellationToken cancellationToken)
    {
        var destination = EnsureDirectory(context.Destination);
        var document = context.Document with { Differences = ImmutableArray<ReportDifference>.Empty };
        await using var stream = File.Create(destination);
        await JsonSerializer.SerializeAsync(stream, document, _options, cancellationToken);
    }

    private static string EnsureDirectory(string path)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory());
        return fullPath;
    }
}
