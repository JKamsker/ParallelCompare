using System;
using System.Diagnostics;
using FluentAssertions;
using ParallelCompare.App.Services;
using Xunit;

namespace ParallelCompare.Tests.Services;

public sealed class DiffToolLauncherTests
{
    [Fact]
    public void TryLaunch_WithoutConfiguredTool_ReturnsFailure()
    {
        var launcher = new DiffToolLauncher(static _ => true, new StubProcessRunner());
        var (success, message) = launcher.TryLaunch(null, "left.txt", "right.txt");

        success.Should().BeFalse();
        message.Should().Be("No diff tool configured.");
    }

    [Fact]
    public void TryLaunch_WhenFilesMissing_ReturnsFailure()
    {
        var launcher = new DiffToolLauncher(static _ => false, new StubProcessRunner());
        var (success, message) = launcher.TryLaunch("tool", "left.txt", "right.txt");

        success.Should().BeFalse();
        message.Should().Be("Both files must exist to launch the diff tool.");
    }

    [Fact]
    public void TryLaunch_WhenProcessStarts_ReturnsSuccess()
    {
        var runner = new StubProcessRunner { Result = new Process() };
        var launcher = new DiffToolLauncher(static _ => true, runner);

        var (success, message) = launcher.TryLaunch("tool", "left.txt", "right.txt");

        success.Should().BeTrue();
        message.Should().Contain("Launched diff tool");
        runner.LastStartInfo.Should().NotBeNull();
        runner.LastStartInfo!.ArgumentList.Should().Contain(new[] { "left.txt", "right.txt" });
    }

    [Fact]
    public void TryLaunch_WhenProcessRunnerThrows_ReturnsFailure()
    {
        var runner = new StubProcessRunner { ThrowOnStart = true };
        var launcher = new DiffToolLauncher(static _ => true, runner);

        var (success, message) = launcher.TryLaunch("tool", "left.txt", "right.txt");

        success.Should().BeFalse();
        message.Should().Contain("Failed to launch diff tool");
    }

    private sealed class StubProcessRunner : IProcessRunner
    {
        public Process? Result { get; init; }

        public bool ThrowOnStart { get; init; }

        public ProcessStartInfo? LastStartInfo { get; private set; }

        public Process? Start(ProcessStartInfo startInfo)
        {
            LastStartInfo = startInfo;
            if (ThrowOnStart)
            {
                throw new InvalidOperationException("boom");
            }

            return Result;
        }
    }
}
