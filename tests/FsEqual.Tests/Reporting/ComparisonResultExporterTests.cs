using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FsEqual.Core.Comparison;
using FsEqual.Core.Options;
using FsEqual.Core.Reporting;
using Xunit;

namespace FsEqual.Tests.Reporting;

public sealed class ComparisonResultExporterTests : IDisposable
{
    private static readonly DateTimeOffset FixedNow = new(2024, 01, 02, 03, 04, 05, TimeSpan.Zero);
    private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private readonly ReportDocumentBuilder _builder = new(() => FixedNow);

    public ComparisonResultExporterTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task JsonExporter_WritesMetadataSummaryAndDifferences()
    {
        var exporter = new JsonReportExporter();
        var (result, resolved) = CreateSampleResult();
        var document = _builder.Build(result, resolved);
        var path = Path.Combine(_root, "exports", "result.json");
        var context = new ReportExportContext(result, resolved, document, path);

        await exporter.ExportAsync(context, CancellationToken.None);

        File.Exists(path).Should().BeTrue();
        using var documentReader = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        var rootElement = documentReader.RootElement;
        var generatedAt = rootElement.GetProperty("metadata").GetProperty("generatedAt").GetString();
        DateTimeOffset.Parse(generatedAt!).Should().Be(FixedNow);
        rootElement.GetProperty("metadata").GetProperty("leftPath").GetString().Should().Be(result.LeftPath);
        rootElement.GetProperty("summary").GetProperty("totalFiles").GetInt32().Should().Be(result.Summary.TotalFiles);
        rootElement.GetProperty("differences").EnumerateArray().Should().HaveCount(2);
    }

    [Fact]
    public async Task SummaryExporter_WritesEmptyDifferences()
    {
        var exporter = new SummaryReportExporter();
        var (result, resolved) = CreateSampleResult();
        var document = _builder.Build(result, resolved);
        var path = Path.Combine(_root, "exports", "summary.json");
        var context = new ReportExportContext(result, resolved, document, path);

        await exporter.ExportAsync(context, CancellationToken.None);

        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        json.RootElement.GetProperty("differences").EnumerateArray().Should().BeEmpty();
        json.RootElement.GetProperty("summary").GetProperty("differentFiles").GetInt32().Should().Be(result.Summary.DifferentFiles);
    }

    [Fact]
    public async Task CsvExporter_IncludesMetadataSection()
    {
        var exporter = new CsvReportExporter();
        var (result, resolved) = CreateSampleResult();
        var document = _builder.Build(result, resolved);
        var path = Path.Combine(_root, "exports", "result.csv");
        var context = new ReportExportContext(result, resolved, document, path);

        await exporter.ExportAsync(context, CancellationToken.None);

        var lines = await File.ReadAllLinesAsync(path);
        lines.Should().Contain(line => line.StartsWith("# metadata", StringComparison.Ordinal));
        lines.Should().Contain(line => line.StartsWith("generatedAt", StringComparison.Ordinal));
        lines.Should().Contain(line => line.Contains("differences", StringComparison.Ordinal));
        lines.Should().Contain(line => line.Contains("Path,Type,Status", StringComparison.Ordinal));
    }

    private static (ComparisonResult Result, ResolvedCompareSettings Resolved) CreateSampleResult()
    {
        var detail = new FileComparisonDetail(
            10,
            12,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            new Dictionary<HashAlgorithmType, string>
            {
                [HashAlgorithmType.Crc32] = "crc-left",
                [HashAlgorithmType.Sha256] = "sha-left"
            },
            new Dictionary<HashAlgorithmType, string>
            {
                [HashAlgorithmType.Crc32] = "crc-right",
                [HashAlgorithmType.Sha256] = "sha-right"
            },
            null);

        var child = new ComparisonNode(
            "file.txt",
            "file.txt",
            ComparisonNodeType.File,
            ComparisonStatus.Different,
            detail,
            ImmutableArray<ComparisonNode>.Empty);

        var root = new ComparisonNode(
            "root",
            string.Empty,
            ComparisonNodeType.Directory,
            ComparisonStatus.Different,
            null,
            ImmutableArray.Create(child));

        var summary = new ComparisonSummary(1, 0, 1, 0, 0, 0);
        var result = new ComparisonResult("/left", "/right", root, summary);

        var resolved = new ResolvedCompareSettings
        {
            LeftPath = "/left",
            RightPath = "/right",
            Mode = ComparisonMode.Quick,
            Algorithms = ImmutableArray.Create(HashAlgorithmType.Crc32, HashAlgorithmType.Sha256),
            IgnorePatterns = ImmutableArray<string>.Empty,
            CaseSensitive = false,
            FollowSymlinks = false,
            ModifiedTimeTolerance = null,
            Threads = null,
            BaselinePath = null,
            JsonReportPath = null,
            SummaryReportPath = null,
            CsvReportPath = null,
            MarkdownReportPath = null,
            HtmlReportPath = null,
            ExportFormat = null,
            NoProgress = false,
            DiffTool = null,
            Verbosity = null,
            FailOn = null,
            Timeout = null,
            InteractiveTheme = null,
            InteractiveFilter = null,
            InteractiveVerbosity = null,
            UsesBaseline = false,
            BaselineMetadata = null
        };

        return (result, resolved);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
