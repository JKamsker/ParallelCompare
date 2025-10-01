using System;
using System.Collections.Immutable;
using FluentAssertions;
using FsEqual.Core.Configuration;
using FsEqual.Core.Options;
using Xunit;

namespace FsEqual.Tests.Configuration;

public sealed class CompareSettingsResolverTests
{
    [Fact]
    public void Resolve_WithCliOverrides_MergesAllSources()
    {
        var configuration = new FsEqualConfiguration
        {
            Defaults = new CompareProfile
            {
                Left = "defaults/left",
                Right = "defaults/right",
                Mode = "hash",
                Algorithms = new[] { "sha256" },
                Ignore = new[] { "*.tmp" },
                Threads = 2
            },
            Profiles =
            {
                ["ci"] = new CompareProfile
                {
                    Right = "profile/right",
                    Algorithms = new[] { "md5" },
                    Ignore = new[] { "*.log" },
                    Threads = 4
                }
            }
        };

        var input = new CompareSettingsInput
        {
            LeftPath = "cli/left",
            RightPath = "cli/right",
            Mode = "quick",
            Algorithm = "crc32",
            AdditionalAlgorithms = ImmutableArray.Create("xxhash64"),
            IgnorePatterns = ImmutableArray.Create("build/*"),
            Threads = 8,
            Profile = "ci"
        };

        var resolver = new CompareSettingsResolver();
        var resolved = resolver.Resolve(input, configuration);

        resolved.LeftPath.Should().EndWith("cli/left");
        resolved.RightPath.Should().EndWith("cli/right");
        resolved.Mode.Should().Be(ComparisonMode.Quick);
        resolved.Algorithms.Should().Contain(new[] { HashAlgorithmType.Crc32, HashAlgorithmType.XxHash64 });
        resolved.IgnorePatterns.Should().BeEquivalentTo(new[] { "*.tmp", "*.log", "build/*" });
        resolved.Threads.Should().Be(8);
    }

    [Fact]
    public void Resolve_WithoutRightOrBaseline_Throws()
    {
        var configuration = new FsEqualConfiguration();
        var input = new CompareSettingsInput
        {
            LeftPath = "left"
        };

        var resolver = new CompareSettingsResolver();
        Action act = () => resolver.Resolve(input, configuration);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Right path must be specified either via CLI or configuration unless a baseline manifest is provided.*");
    }

    [Fact]
    public void Resolve_WithMissingProfile_Throws()
    {
        var configuration = new FsEqualConfiguration();
        var input = new CompareSettingsInput
        {
            LeftPath = "left",
            RightPath = "right",
            Profile = "missing"
        };

        var resolver = new CompareSettingsResolver();
        Action act = () => resolver.Resolve(input, configuration);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Profile 'missing' was not found in the configuration file.");
    }

    [Fact]
    public void Resolve_WithConfigurationDebounce_UsesConfiguredValue()
    {
        var configuration = new FsEqualConfiguration
        {
            Defaults = new CompareProfile
            {
                Left = "defaults/left",
                Right = "defaults/right",
                DebounceMilliseconds = 1500
            }
        };

        var input = new CompareSettingsInput
        {
            LeftPath = "defaults/left",
            RightPath = "defaults/right"
        };

        var resolver = new CompareSettingsResolver();
        var resolved = resolver.Resolve(input, configuration);

        resolved.WatchDebounceMilliseconds.Should().Be(1500);
    }

    [Fact]
    public void Resolve_WithCliDebounce_OverridesConfiguration()
    {
        var configuration = new FsEqualConfiguration
        {
            Defaults = new CompareProfile
            {
                Left = "defaults/left",
                Right = "defaults/right",
                DebounceMilliseconds = 2000
            },
            Profiles =
            {
                ["ci"] = new CompareProfile
                {
                    DebounceMilliseconds = 1400
                }
            }
        };

        var input = new CompareSettingsInput
        {
            LeftPath = "cli/left",
            RightPath = "cli/right",
            Profile = "ci",
            DebounceMilliseconds = 320
        };

        var resolver = new CompareSettingsResolver();
        var resolved = resolver.Resolve(input, configuration);

        resolved.WatchDebounceMilliseconds.Should().Be(320);
    }
}
