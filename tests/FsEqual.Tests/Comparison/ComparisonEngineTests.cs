using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FsEqual.Core.Comparison;
using FsEqual.Core.Options;
using FsEqual.Tests.Infrastructure;
using Xunit;

namespace FsEqual.Tests.Comparison;

public sealed class ComparisonEngineTests
{
    private const string BaseDirectoryJson = """
    {
      "files": {
        "file1.txt": "content of file 1",
        "file2.txt": "content of file 2",
        "file3.bin": { "seed": 12345, "size": 1024 },
        "empty.txt": ""
      },
      "directories": {
        "nested": {
          "files": {
            "deep.txt": "deep content"
          },
          "directories": {
            "level2": {
              "files": {
                "level2.bin": { "seed": 77, "size": 2048 }
              }
            }
          }
        }
      }
    }
    """;

    [Fact]
    public async Task Compare_WithIdenticalStructures_ReportsNoDifferences()
    {
        var options = BuildOptions(static template => template, static template => template);

        var engine = new ComparisonEngine();
        var result = await engine.CompareAsync(options);

        result.Summary.DifferentFiles.Should().Be(0);
        result.Summary.LeftOnlyFiles.Should().Be(0);
        result.Summary.RightOnlyFiles.Should().Be(0);
        result.Summary.ErrorFiles.Should().Be(0);
        result.Root.Children.Should().OnlyContain(child => child.Status == ComparisonStatus.Equal);
    }

    [Fact]
    public async Task Compare_WhenFileRemovedFromRight_IsReportedAsLeftOnly()
    {
        var options = BuildOptions(
            static template => template,
            template =>
            {
                var clone = template.Clone();
                clone.RemoveEntry("file2.txt");
                return clone;
            });

        var engine = new ComparisonEngine();
        var result = await engine.CompareAsync(options);

        result.Summary.LeftOnlyFiles.Should().Be(1);
        result.Root.Children.Should().Contain(node => node.Name == "file2.txt" && node.Status == ComparisonStatus.LeftOnly);
    }

    [Fact]
    public async Task Compare_WhenTextFileChanged_IsReportedAsDifferent()
    {
        var options = BuildOptions(
            static template => template,
            template =>
            {
                var clone = template.Clone();
                clone.UpdateTextFile("file1.txt", "modified content");
                return clone;
            });

        var engine = new ComparisonEngine();
        var result = await engine.CompareAsync(options);

        result.Summary.DifferentFiles.Should().Be(1);
        result.Root.Children.Should().Contain(node => node.Name == "file1.txt" && node.Status == ComparisonStatus.Different);
    }

    [Fact]
    public async Task Compare_WhenBinarySeedDiffers_IsReportedAsDifferent()
    {
        var options = BuildOptions(
            static template => template,
            template =>
            {
                var clone = template.Clone();
                clone.UpdateBinaryFile("file3.bin", seed: 54321);
                return clone;
            });

        var engine = new ComparisonEngine();
        var result = await engine.CompareAsync(options);

        result.Summary.DifferentFiles.Should().Be(1);
        result.Root.Children.Should().Contain(node => node.Name == "file3.bin" && node.Status == ComparisonStatus.Different);
    }

    [Fact]
    public async Task Compare_WhenDirectoryRemovedFromRight_IsReportedAsLeftOnly()
    {
        var options = BuildOptions(
            static template => template,
            template =>
            {
                var clone = template.Clone();
                clone.RemoveEntry(Path.Combine("nested", "level2"));
                return clone;
            });

        var engine = new ComparisonEngine();
        var result = await engine.CompareAsync(options);

        result.Summary.LeftOnlyFiles.Should().BeGreaterThan(0);
        var nested = result.Root.Children.Single(node => node.Name == "nested");
        nested.Status.Should().Be(ComparisonStatus.LeftOnly);
    }

    [Fact]
    public async Task Compare_WhenFileRemovedFromLeft_IsReportedAsRightOnly()
    {
        var options = BuildOptions(
            template =>
            {
                var clone = template.Clone();
                clone.RemoveEntry("file2.txt");
                return clone;
            },
            static template => template);

        var engine = new ComparisonEngine();
        var result = await engine.CompareAsync(options);

        result.Summary.RightOnlyFiles.Should().Be(1);
        result.Root.Children.Should().Contain(node => node.Name == "file2.txt" && node.Status == ComparisonStatus.RightOnly);
    }

    private static ComparisonOptions BuildOptions(
        Func<DeterministicFileSystem.DeterministicFileSystemTemplate, DeterministicFileSystem.DeterministicFileSystemTemplate> leftTransformer,
        Func<DeterministicFileSystem.DeterministicFileSystemTemplate, DeterministicFileSystem.DeterministicFileSystemTemplate> rightTransformer)
    {
        var baseTemplate = DeterministicFileSystem.DeterministicFileSystemTemplate.FromJson(BaseDirectoryJson);
        var leftTemplate = leftTransformer(baseTemplate.Clone());
        var rightTemplate = rightTransformer(baseTemplate.Clone());

        var fileSystem = DeterministicFileSystem.Compose(new[]
        {
            (Path.Combine(Path.DirectorySeparatorChar.ToString(), "left"), leftTemplate),
            (Path.Combine(Path.DirectorySeparatorChar.ToString(), "right"), rightTemplate)
        }, caseSensitive: true);

        return new ComparisonOptions
        {
            LeftPath = Path.Combine(Path.DirectorySeparatorChar.ToString(), "left"),
            RightPath = Path.Combine(Path.DirectorySeparatorChar.ToString(), "right"),
            Mode = ComparisonMode.Quick,
            HashAlgorithms = ImmutableArray<HashAlgorithmType>.Empty,
            IgnorePatterns = ImmutableArray<string>.Empty,
            CaseSensitive = true,
            FileSystem = fileSystem
        };
    }
}
