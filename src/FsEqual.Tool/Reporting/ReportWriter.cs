using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using FsEqual.Tool.Commands;
using FsEqual.Tool.Core;

namespace FsEqual.Tool.Reporting;

internal static class ReportWriter
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static async Task WriteReportsAsync(ComparisonResult result, ResolvedCompareSettings settings, CancellationToken token)
    {
        if (settings.JsonOutput is not null)
        {
            await WriteDetailedAsync(result, settings, token);
        }

        if (settings.SummaryOutput is not null)
        {
            await WriteSummaryAsync(result, settings, token);
        }
    }

    private static async Task WriteDetailedAsync(ComparisonResult result, ResolvedCompareSettings settings, CancellationToken token)
    {
        var payload = new DetailedReport
        {
            Left = Path.GetFullPath(settings.LeftPath),
            Right = Path.GetFullPath(settings.RightPath),
            Mode = settings.Mode.ToString(),
            Algorithm = settings.Algorithm.ToString(),
            Duration = result.Duration,
            Summary = MapSummary(result.Summary),
            Differences = result.Differences.Select(MapDifference).ToArray(),
            Errors = result.Errors.Select(e => new ErrorEntry(e.Scope, e.Message)).ToArray(),
            GeneratedAtUtc = DateTimeOffset.UtcNow,
        };

        await WriteJsonAsync(settings.JsonOutput!, payload, token);
    }

    private static async Task WriteSummaryAsync(ComparisonResult result, ResolvedCompareSettings settings, CancellationToken token)
    {
        var payload = new SummaryReport
        {
            Left = Path.GetFullPath(settings.LeftPath),
            Right = Path.GetFullPath(settings.RightPath),
            Duration = result.Duration,
            Outcome = result.Outcome.ToString(),
            Summary = MapSummary(result.Summary),
            GeneratedAtUtc = DateTimeOffset.UtcNow,
        };

        await WriteJsonAsync(settings.SummaryOutput!, payload, token);
    }

    private static async Task WriteJsonAsync(string path, object payload, CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? Environment.CurrentDirectory);
        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, payload, Options, token);
    }

    private static SummaryEntry MapSummary(ComparisonSummary summary)
    {
        return new SummaryEntry
        {
            FilesCompared = summary.FilesCompared,
            DirectoriesCompared = summary.DirectoriesCompared,
            EqualFiles = summary.EqualFiles,
            EqualDirectories = summary.EqualDirectories,
            MissingLeft = summary.MissingLeft,
            MissingRight = summary.MissingRight,
            TypeMismatches = summary.TypeMismatches,
            SizeMismatches = summary.SizeMismatches,
            HashMismatches = summary.HashMismatches,
            MetadataMismatches = summary.MetadataMismatches,
            Errors = summary.ErrorCount,
        };
    }

    private static DifferenceEntry MapDifference(FileDifference difference)
    {
        return new DifferenceEntry
        {
            Type = difference.Type.ToString(),
            Path = difference.RelativePath,
            Detail = difference.Detail,
            Algorithm = difference.Algorithm?.ToString(),
            Left = difference.Left is null ? null : MapMetadata(difference.Left),
            Right = difference.Right is null ? null : MapMetadata(difference.Right),
        };
    }

    private static MetadataEntry MapMetadata(FileMetadata metadata)
    {
        return new MetadataEntry
        {
            Path = metadata.FullPath,
            Size = metadata.IsDirectory ? null : metadata.Length,
            LastWriteTimeUtc = metadata.LastWriteTime,
            Hash = metadata.Hash,
            IsDirectory = metadata.IsDirectory,
            IsSymlink = metadata.IsSymlink,
        };
    }

    private sealed record SummaryReport
    {
        public required string Left { get; init; }

        public required string Right { get; init; }

        public required TimeSpan Duration { get; init; }

        public required string Outcome { get; init; }

        public required SummaryEntry Summary { get; init; }

        public required DateTimeOffset GeneratedAtUtc { get; init; }
    }

    private sealed record DetailedReport
    {
        public required string Left { get; init; }

        public required string Right { get; init; }

        public required string Mode { get; init; }

        public required string Algorithm { get; init; }

        public required TimeSpan Duration { get; init; }

        public required SummaryEntry Summary { get; init; }

        public required IReadOnlyList<DifferenceEntry> Differences { get; init; }

        public required IReadOnlyList<ErrorEntry> Errors { get; init; }

        public required DateTimeOffset GeneratedAtUtc { get; init; }
    }

    private sealed record SummaryEntry
    {
        public required int FilesCompared { get; init; }

        public required int DirectoriesCompared { get; init; }

        public required int EqualFiles { get; init; }

        public required int EqualDirectories { get; init; }

        public required int MissingLeft { get; init; }

        public required int MissingRight { get; init; }

        public required int TypeMismatches { get; init; }

        public required int SizeMismatches { get; init; }

        public required int HashMismatches { get; init; }

        public required int MetadataMismatches { get; init; }

        public required int Errors { get; init; }
    }

    private sealed record DifferenceEntry
    {
        public required string Type { get; init; }

        public required string Path { get; init; }

        public string? Detail { get; init; }

        public string? Algorithm { get; init; }

        public MetadataEntry? Left { get; init; }

        public MetadataEntry? Right { get; init; }
    }

    private sealed record MetadataEntry
    {
        public required string Path { get; init; }

        public long? Size { get; init; }

        public required DateTimeOffset LastWriteTimeUtc { get; init; }

        public string? Hash { get; init; }

        public required bool IsDirectory { get; init; }

        public required bool IsSymlink { get; init; }
    }

    private sealed record ErrorEntry(string Scope, string Message);
}
