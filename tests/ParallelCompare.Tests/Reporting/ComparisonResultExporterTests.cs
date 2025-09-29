using System;
using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using ParallelCompare.Core.Comparison;
using ParallelCompare.Core.Reporting;
using Xunit;

namespace ParallelCompare.Tests.Reporting;

public sealed class ComparisonResultExporterTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public ComparisonResultExporterTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task WriteJsonAsync_CreatesDirectoryAndWritesPayload()
    {
        var exporter = new ComparisonResultExporter();
        var result = CreateSampleResult();
        var path = Path.Combine(_root, "exports", "result.json");

        await exporter.WriteJsonAsync(result, path, CancellationToken.None);

        File.Exists(path).Should().BeTrue();
        var json = await File.ReadAllTextAsync(path);
        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("leftPath").GetString().Should().Be(result.LeftPath);
        document.RootElement.GetProperty("root").GetProperty("children").EnumerateArray().Should().HaveCount(1);
    }

    [Fact]
    public async Task WriteSummaryAsync_CreatesDirectoryAndWritesSummary()
    {
        var exporter = new ComparisonResultExporter();
        var result = CreateSampleResult();
        var path = Path.Combine(_root, "exports", "summary.json");

        await exporter.WriteSummaryAsync(result, path, CancellationToken.None);

        File.Exists(path).Should().BeTrue();
        var json = await File.ReadAllTextAsync(path);
        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("totalFiles").GetInt32().Should().Be(result.Summary.TotalFiles);
    }

    private static ComparisonResult CreateSampleResult()
    {
        var child = new ComparisonNode(
            "file.txt",
            "file.txt",
            ComparisonNodeType.File,
            ComparisonStatus.Equal,
            new FileComparisonDetail(10, 10, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, null, null),
            ImmutableArray<ComparisonNode>.Empty);

        var root = new ComparisonNode(
            "left",
            string.Empty,
            ComparisonNodeType.Directory,
            ComparisonStatus.Equal,
            null,
            ImmutableArray.Create(child));

        var summary = new ComparisonSummary(1, 1, 0, 0, 0, 0);
        return new ComparisonResult("/left", "/right", root, summary);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
