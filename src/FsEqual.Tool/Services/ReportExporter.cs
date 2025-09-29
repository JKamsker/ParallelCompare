using System.Text.Json;
using FsEqual.Tool.Models;

namespace FsEqual.Tool.Services;

public sealed class ReportExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public async Task ExportFullAsync(ComparisonReport report, string path, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, report, JsonOptions, cancellationToken);
    }

    public async Task ExportSummaryAsync(ComparisonReport report, string path, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, report.Summary, JsonOptions, cancellationToken);
    }
}
