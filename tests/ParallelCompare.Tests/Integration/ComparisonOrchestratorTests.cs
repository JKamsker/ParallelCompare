using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using ParallelCompare.App.Commands;
using ParallelCompare.App.Services;
using ParallelCompare.Core.Comparison;
using ParallelCompare.Core.Options;
using Xunit;

namespace ParallelCompare.Tests.Integration;

public sealed class ComparisonOrchestratorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public ComparisonOrchestratorTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task RunAsync_StandardComparisonDetectsDifferences()
    {
        var left = CreateDirectory("left", new PhysicalFile
        {
            RelativePath = "file1.txt",
            Content = "hello"
        }, new PhysicalFile
        {
            RelativePath = Path.Combine("nested", "file2.txt"),
            Content = "content"
        });

        var right = CreateDirectory("right", new PhysicalFile
        {
            RelativePath = "file1.txt",
            Content = "hello"
        });

        var orchestrator = new ComparisonOrchestrator();
        var input = new CompareSettingsInput
        {
            LeftPath = left,
            RightPath = right
        };

        var (result, resolved) = await orchestrator.RunAsync(input, CancellationToken.None);

        resolved.UsesBaseline.Should().BeFalse();
        result.Summary.LeftOnlyFiles.Should().Be(1);
        result.Root.Children.Should().Contain(node => node.Name == "nested" && node.Status == ComparisonStatus.LeftOnly);
    }

    [Fact]
    public async Task RunAsync_WithBaselineManifestUsesSnapshot()
    {
        var left = CreateDirectory("left-baseline", new PhysicalFile
        {
            RelativePath = "file.txt",
            Content = "baseline"
        });

        var orchestrator = new ComparisonOrchestrator();
        var input = new CompareSettingsInput
        {
            LeftPath = left,
            RightPath = left
        };

        var manifestPath = Path.Combine(_root, "baseline.json");
        await orchestrator.CreateSnapshotAsync(input, manifestPath, CancellationToken.None);

        // Mutate the directory after snapshot.
        File.WriteAllText(Path.Combine(left, "file.txt"), "changed");

        var compareInput = new CompareSettingsInput
        {
            LeftPath = left,
            BaselinePath = manifestPath
        };

        var (result, resolved) = await orchestrator.RunAsync(compareInput, CancellationToken.None);

        resolved.UsesBaseline.Should().BeTrue();
        result.Baseline.Should().NotBeNull();
        result.Summary.DifferentFiles.Should().Be(1);
    }

    [Fact]
    public async Task CreateSnapshotAsync_WritesManifestWithEntries()
    {
        var left = CreateDirectory("snapshot", new PhysicalFile
        {
            RelativePath = Path.Combine("sub", "file.txt"),
            Content = "snapshot"
        });

        var orchestrator = new ComparisonOrchestrator();
        var input = new CompareSettingsInput
        {
            LeftPath = left,
            RightPath = left
        };

        var manifestPath = Path.Combine(_root, "snapshot.json");
        var manifest = await orchestrator.CreateSnapshotAsync(input, manifestPath, CancellationToken.None);

        File.Exists(manifestPath).Should().BeTrue();
        manifest.Root.Children.Should().Contain(entry => entry.Name == "sub");

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
        document.RootElement.GetProperty("root").GetProperty("children").EnumerateArray().Should().HaveCount(1);
    }

    [Fact]
    public async Task WatchCommand_RespectsTimeoutCancellation()
    {
        var left = CreateDirectory("watch-left", new PhysicalFile
        {
            RelativePath = "file.txt",
            Content = "watch"
        });

        var right = CreateDirectory("watch-right", new PhysicalFile
        {
            RelativePath = "file.txt",
            Content = "watch"
        });

        var orchestrator = new ComparisonOrchestrator();
        var command = new WatchCommand(orchestrator);
        var settings = new WatchCommandSettings
        {
            Left = left,
            Right = right,
            TimeoutSeconds = 1
        };

        var original = Environment.GetEnvironmentVariable("SPECTRE_CONSOLE_DISABLE");
        Environment.SetEnvironmentVariable("SPECTRE_CONSOLE_DISABLE", "true");
        try
        {
            var exitCode = await command.ExecuteAsync(null!, settings);
            exitCode.Should().Be(0);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SPECTRE_CONSOLE_DISABLE", original);
        }
    }

    private string CreateDirectory(string name, params PhysicalFile[] files)
    {
        var directory = Path.Combine(_root, name);
        Directory.CreateDirectory(directory);
        foreach (var file in files)
        {
            var path = Path.Combine(directory, file.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (file.BinarySeed.HasValue)
            {
                var data = new byte[file.BinarySize];
                var random = new Random(file.BinarySeed.Value);
                random.NextBytes(data);
                File.WriteAllBytes(path, data);
            }
            else
            {
                File.WriteAllText(path, file.Content ?? string.Empty);
            }
        }

        return directory;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed record PhysicalFile
    {
        public required string RelativePath { get; init; }
        public string? Content { get; init; }
        public int? BinarySeed { get; init; }
        public int BinarySize { get; init; } = 256;
    }
}
