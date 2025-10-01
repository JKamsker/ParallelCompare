using System;
using System.Collections.Immutable;
using System.IO;
using FluentAssertions;
using FsEqual.App.Commands;
using FsEqual.Core.Comparison;
using FsEqual.Core.Configuration;
using FsEqual.Core.Options;
using Xunit;

namespace FsEqual.Tests.Commands;

public sealed class WatchCommandTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public WatchCommandTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void ResolveDebounceInterval_UsesCliValueWhenProvided()
    {
        var settings = new WatchCommandSettings
        {
            DebounceMilliseconds = 125
        };

        var resolved = CreateResolved(leftName: "cli-left", rightName: "cli-right");

        var interval = WatchCommand.ResolveDebounceInterval(settings, resolved);

        interval.Should().Be(TimeSpan.FromMilliseconds(125));
    }

    [Fact]
    public void ResolveDebounceInterval_FallsBackToResolvedValue()
    {
        var settings = new WatchCommandSettings();
        var resolved = CreateResolved(leftName: "config-left", rightName: "config-right")
            with
            {
                WatchDebounceMilliseconds = 640
            };

        var interval = WatchCommand.ResolveDebounceInterval(settings, resolved);

        interval.Should().Be(TimeSpan.FromMilliseconds(640));
    }

    [Fact]
    public void ResolveDebounceInterval_DefaultsWhenNotSpecified()
    {
        var settings = new WatchCommandSettings();
        var resolved = CreateResolved(leftName: "default-left", rightName: "default-right");

        var interval = WatchCommand.ResolveDebounceInterval(settings, resolved);

        interval.Should().Be(TimeSpan.FromMilliseconds(WatchCommandSettings.DefaultDebounceMilliseconds));
    }

    [Fact]
    public void ShouldTriggerChange_SuppressesCliIgnoredPath()
    {
        var left = CreateDirectory("cli-ignore-left");
        var right = CreateDirectory("cli-ignore-right");

        var input = new CompareSettingsInput
        {
            LeftPath = left,
            RightPath = right,
            IgnorePatterns = ImmutableArray.Create("*.tmp")
        };

        var resolver = new CompareSettingsResolver();
        var resolved = resolver.Resolve(input, configuration: null);
        var matcher = WatchCommand.CreateIgnoreMatcher(resolved)!;

        var ignoredFile = Path.Combine(left, "skip.tmp");
        WatchCommand.ShouldTriggerChange(left, ignoredFile, null, matcher).Should().BeFalse();

        var includedFile = Path.Combine(left, "keep.txt");
        WatchCommand.ShouldTriggerChange(left, includedFile, null, matcher).Should().BeTrue();
    }

    [Fact]
    public void ShouldTriggerChange_SuppressesConfigIgnoredPath()
    {
        var left = CreateDirectory("config-ignore-left");
        var right = CreateDirectory("config-ignore-right");

        var configuration = new FsEqualConfiguration
        {
            Defaults = new CompareProfile
            {
                Left = left,
                Right = right,
                Ignore = new[] { "*.log" }
            }
        };

        var input = new CompareSettingsInput
        {
            LeftPath = left,
            RightPath = right
        };

        var resolver = new CompareSettingsResolver();
        var resolved = resolver.Resolve(input, configuration);
        var matcher = WatchCommand.CreateIgnoreMatcher(resolved)!;

        var ignoredFile = Path.Combine(left, "skip.log");
        WatchCommand.ShouldTriggerChange(left, ignoredFile, null, matcher).Should().BeFalse();

        var includedFile = Path.Combine(left, "keep.txt");
        WatchCommand.ShouldTriggerChange(left, includedFile, null, matcher).Should().BeTrue();
    }

    private ResolvedCompareSettings CreateResolved(string leftName, string rightName)
    {
        var left = CreateDirectory(leftName);
        var right = CreateDirectory(rightName);

        return new ResolvedCompareSettings
        {
            LeftPath = left,
            RightPath = right,
            Mode = ComparisonMode.Quick
        };
    }

    private string CreateDirectory(string name)
    {
        var path = Path.Combine(_root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
